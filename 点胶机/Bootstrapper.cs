using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using 点胶机.Core.Config;
using 点胶机.Core.Events;
using 点胶机.Core.Interfaces;
using 点胶机.Hardware;
using 点胶机.Hardware.Devices;
using 点胶机.Logging;
using 点胶机.ViewModels;

namespace 点胶机;

/// <summary>
/// 启动引导 —— 对齐 AutoStudio.Bootstrapper
/// 流程:读 appsettings → 配置 Serilog → 构建 DI → 初始化硬件 → 注册任务 → 显示 Shell
/// (第1阶段只完成配置/Serilog/DI/Shell,后续阶段补硬件/任务/数据库)
/// </summary>
public class Bootstrapper
{
    public ServiceProvider Services { get; private set; } = null!;
    public AppConfig Config { get; private set; } = null!;

    public async Task StartupAsync()
    {
        // 1. 读 appsettings.json
        LoadConfiguration();

        // 2. 配置 Serilog(File Sink;MySQL Sink 第5阶段挂载)
        SerilogConfig.Configure(Config.Logging);

        Log.Information("===== 点胶机自动控制系统启动 =====");
        Log.Information("机器: {Name} {Version}", Config.Machine.Name, Config.Machine.Version);

        // 3. 构建 DI 容器
        ConfigureServices();

        // 4. 初始化硬件
        var hardware = Services.GetRequiredService<HardwareManager>();
        hardware.Init();

        // 4.1 初始化数据库(MySQL 建库建表)+ 挂载 MySQL 日志 Sink
        bool dbOk = Data.DbInitializer.Initialize(Config.Database.ConnectionString, Config.Database.AutoCreateTable);
        if (dbOk)
        {
            Logging.SerilogConfig.AttachMySqlSink(new Data.MySqlLogSink(Config.Database.ConnectionString));
            Log.Information("MySQL 数据库初始化成功,日志/报警/生产记录将落库");
        }
        else
        {
            Log.Warning("MySQL 不可用,降级为仅文件日志");
        }

        // 把事件总线注入全局状态(用于发布状态变化)
        Engine.TaskStatic.Instance.EventBus = Services.GetRequiredService<IEventBus>();

        // 5. 加载配方
        var recipe = Services.GetRequiredService<Recipe.RecipeManager>();
        recipe.Load();
        Log.Information("配方已加载: {Name}, 轨迹点 {N}", recipe.Current.Name, recipe.Current.Points.Count);

        // 6. 注册任务到调度器并启动
        var scheduler = Services.GetRequiredService<Engine.TaskScheduler>();
        scheduler
            .SetThreadNumber(Config.Scheduler.ThreadCount)
            .SetThreadSleepTime(Config.Scheduler.TickIntervalMs)
            .AddTask(Services.GetRequiredService<Tasks.Task_SystemInit>(), true)
            .AddTask(Services.GetRequiredService<Tasks.Task_Dispensing>(), true)
            .AddTask(Services.GetRequiredService<Tasks.Task_AlarmMonitor>(), true)
            .AddTask(Services.GetRequiredService<Tasks.Task_IoWatchdog>(), false)
            .Start();

        // 6.1 订阅报警事件 → 弹窗(DialogService 订阅 AlarmEvent)
        var dialog = Services.GetRequiredService<IDialogService>();
        Services.GetRequiredService<IEventBus>().Subscribe<Core.Events.AlarmEvent>(evt =>
        {
            if (evt.IsActive)
                dialog.ShowAlarm(evt.AlarmId, evt.Name, evt.Level, evt.Message ?? "");
        });

        // 7. 显示 Shell(启动壳)
        var shell = Services.GetRequiredService<MainWindow>();
        shell.Show();

        Log.Information("Shell 已显示,系统就绪");

        // ===== 联调自启动测试(初始化就绪后自动启动,跑若干片后触发报警验证落库)=====
        // 由 appsettings.AutoStartForTest 控制(默认 true,用于阶段7验证;生产改 false)
        if (Config.AutoStartForTest)
        {
            _ = Task.Run(async () =>
            {
                // 等待初始化完成(最多 10 秒)
                for (int i = 0; i < 100; i++)
                {
                    if (Engine.TaskStatic.Instance.ReadyStatus == Core.Enums.ReadyStatus.Initialized) break;
                    await Task.Delay(100);
                }
                if (Engine.TaskStatic.Instance.ReadyStatus == Core.Enums.ReadyStatus.Initialized)
                {
                    Log.Information("===== [联调测试] 自动启动点胶流程 =====");
                    Engine.TaskStatic.Instance.StartButton = true;

                    // 等 8 秒(约跑 2 片)后触发气压故障报警,验证报警落库 + 弹窗
                    await Task.Delay(8000);
                    Log.Information("===== [联调测试] 触发气压故障报警(验证报警系统) =====");
                    var hw = Services.GetRequiredService<HardwareManager>();
                    if (hw.Pressure is Hardware.Devices.PressureSensorSimulator ps)
                        ps.SetFaultChance(0.9);
                }
            });
        }

        await Task.CompletedTask;
    }

    private void LoadConfiguration()
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        Config = new AppConfig();
        cfg.GetSection("Machine").Bind(Config.Machine);
        cfg.GetSection("Hardware").Bind(Config.Hardware);
        cfg.GetSection("Database").Bind(Config.Database);
        cfg.GetSection("Logging").Bind(Config.Logging);
        cfg.GetSection("Scheduler").Bind(Config.Scheduler);
        cfg.GetSection("Axes").Bind(Config.Axes);
        // 顶层标量字段
        Config.AutoStartForTest = cfg.GetValue("AutoStartForTest", false);
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // ===== 配置 =====
        services.AddSingleton(Config);

        // ===== 核心服务(第2/3阶段补全具体实现)=====
        services.AddSingleton<IEventBus, EventBus>();

        // ===== 硬件(第3阶段)=====
        services.AddSingleton<HardwareManager>();
        services.AddSingleton<IPlcHardware>(sp => sp.GetRequiredService<HardwareManager>().Plc);
        services.AddSingleton<IMotionCard>(sp => sp.GetRequiredService<HardwareManager>().Motion);
        services.AddSingleton<IGlueValve>(sp => sp.GetRequiredService<HardwareManager>().GlueValve);
        services.AddSingleton<IPressureSensor>(sp => sp.GetRequiredService<HardwareManager>().Pressure);
        // 具体类型注册(供 Task_IoWatchdog 等需要调用 Tick 的任务使用)
        services.AddSingleton(sp => sp.GetRequiredService<HardwareManager>().GlueValve);

        // ===== 报警服务 + 配方(第4阶段)=====
        services.AddSingleton<IAlarmService>(sp => new Alarm.AlarmService(
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<Data.AlarmRepository>()));
        services.AddSingleton<Recipe.RecipeManager>();

        // ===== 数据层(第5阶段)=====
        services.AddSingleton(sp => new Data.LogRepository(sp.GetRequiredService<AppConfig>().Database.ConnectionString));
        services.AddSingleton(sp => new Data.AlarmRepository(sp.GetRequiredService<AppConfig>().Database.ConnectionString));
        services.AddSingleton(sp => new Data.ProductionRepository(sp.GetRequiredService<AppConfig>().Database.ConnectionString));

        // ===== 对话框服务(第6阶段)=====
        services.AddSingleton<IDialogService, Services.DialogService>();

        // ===== 任务调度器 + 业务任务(第4阶段)=====
        services.AddSingleton<Engine.TaskScheduler>();
        services.AddSingleton<Tasks.Task_SystemInit>();
        services.AddSingleton<Tasks.Task_Dispensing>(sp =>
            new Tasks.Task_Dispensing(
                sp.GetRequiredService<HardwareManager>(),
                sp.GetRequiredService<Recipe.RecipeManager>(),
                sp.GetRequiredService<IGlueValve>(),
                sp.GetRequiredService<AppConfig>().Hardware.Vision.SimDelayMs,
                sp.GetRequiredService<Data.ProductionRepository>()));
        services.AddSingleton<Tasks.Task_AlarmMonitor>();
        services.AddSingleton<Tasks.Task_IoWatchdog>();

        // services.AddSingleton<IDialogService, DialogService>(); // 第6阶段

        // ===== ViewModel(导航用,Singleton 复用状态)=====
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<ManualViewModel>();
        services.AddSingleton<AutoViewModel>();
        services.AddSingleton<RecipeViewModel>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<IoViewModel>();
        services.AddSingleton<SettingViewModel>();

        // ===== View =====
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    /// <summary>关闭:停任务线程、刷日志</summary>
    public void Shutdown()
    {
        Log.Information("===== 系统关闭 =====");
        try { Services.GetRequiredService<Engine.TaskScheduler>().Stop(); } catch { }
        try { Services.GetRequiredService<HardwareManager>().Dispose(); } catch { }
        Log.CloseAndFlush();
    }
}
