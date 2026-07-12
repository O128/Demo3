using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Engine;

namespace 点胶机.ViewModels;

/// <summary>
/// 启动壳(MainWindow)的 ViewModel —— 负责导航 + 顶部控制按钮(启动/停止/暂停/急停/复位)
/// 对齐 AutoStudio 的 AppShellViewModel
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly IEventBus _eventBus;

    [ObservableProperty]
    private ObservableObject? _currentView;

    // —— 顶部状态条绑定 ——
    [ObservableProperty] private string _machineName = "Dispenser-01";
    [ObservableProperty] private string _version = "V1.0.1";
    [ObservableProperty] private string _runStatusText = "停止";
    [ObservableProperty] private string _runStatusColor = "Gray";
    [ObservableProperty] private string _workModeText = "Auto";
    [ObservableProperty] private string _readyStatusText = "初始化中...";

    /// <summary>手动页面是否可进入(始终 true —— 页面总能进,只灰运行按钮)</summary>
    [ObservableProperty] private bool _isManualEnabled = true;
    /// <summary>手动页运行类按钮是否可用:仅在"手动模式 且 未运行"时为 true</summary>
    [ObservableProperty] private bool _isRunButtonsEnabled = true;
    /// <summary>自动页运行类按钮是否可用:仅在"自动模式"时为 true(手动模式禁用自动)</summary>
    [ObservableProperty] private bool _isAutoRunEnabled = true;
    /// <summary>手动禁用原因提示(显示在导航项 tooltip / 手动页横幅)</summary>
    [ObservableProperty] private string _manualDisabledReason = "";
    /// <summary>急停按钮颜色:常红(急停按钮始终红色,符合工业安全色规范)</summary>
    [ObservableProperty] private string _estopColor = "Red";

    public ShellViewModel(IServiceProvider services, IEventBus eventBus)
    {
        _services = services;
        _eventBus = eventBus;
        CurrentView = services.GetRequiredService<HomeViewModel>();

        _eventBus.Subscribe<Core.Events.StatusChangedEvent>(OnStatusChanged);
        // 订阅 TaskStatic 属性变化(WorkMode/RunStatus 变化时刷新手动可用性)
        TaskStatic.Instance.PropertyChanged += (_, _) => RefreshStatus();
        RefreshStatus();
    }

    private void OnStatusChanged(Core.Events.StatusChangedEvent e) => RefreshStatus();

    private void RefreshStatus()
    {
        var ts = TaskStatic.Instance;
        RunStatusText = ts.RunStatus switch
        {
            RunStatus.Running => "运行中",
            RunStatus.Paused => "暂停",
            _ => "停止"
        };
        RunStatusColor = ts.RunStatus switch
        {
            RunStatus.Running => "Green",
            RunStatus.Paused => "Orange",
            _ => "Gray"
        };
        WorkModeText = ts.WorkMode.ToString();
        ReadyStatusText = ts.ReadyStatus switch
        {
            ReadyStatus.Initialized => "就绪",
            ReadyStatus.Initializing => "初始化中...",
            _ => "未初始化"
        };

        // 需求:手自动互斥
        // - 手动页始终可进;手动运行类按钮仅在"手动模式 且 未运行"时可用
        // - 自动运行类按钮仅在"自动模式"时可用(手动模式禁用自动)
        IsManualEnabled = true;
        IsRunButtonsEnabled = ts.WorkMode == WorkMode.Manual && ts.RunStatus != RunStatus.Running;
        IsAutoRunEnabled = ts.WorkMode == WorkMode.Auto;
        ManualDisabledReason = (ts.WorkMode == WorkMode.Auto || ts.RunStatus == RunStatus.Running)
            ? (ts.RunStatus == RunStatus.Running
                ? "系统运行中,手动操作已禁用(请先停止)"
                : "当前为自动模式,运行类按钮已禁用(切到手动模式可操作)")
            : "";
        // 急停按钮常红(不再随状态变色)
    }

    // —— 导航命令 ——
    [RelayCommand] private void GoHome() => CurrentView = _services.GetRequiredService<HomeViewModel>();
    [RelayCommand] private void GoManual() { if (IsManualEnabled) CurrentView = _services.GetRequiredService<ManualViewModel>(); }
    [RelayCommand] private void GoAuto() => CurrentView = _services.GetRequiredService<AutoViewModel>();
    [RelayCommand] private void GoRecipe() => CurrentView = _services.GetRequiredService<RecipeViewModel>();
    [RelayCommand] private void GoAlarm() => CurrentView = _services.GetRequiredService<AlarmViewModel>();
    [RelayCommand] private void GoLog() => CurrentView = _services.GetRequiredService<LogViewModel>();
    [RelayCommand] private void GoIo() => CurrentView = _services.GetRequiredService<IoViewModel>();
    [RelayCommand] private void GoSetting() => CurrentView = _services.GetRequiredService<SettingViewModel>();

    // —— 控制按钮(写 TaskStatic 按钮信号,IoWatchdog 任务处理)——
    [RelayCommand] private void Start()
    {
        TaskStatic.Instance.StartButton = true;
        _eventBus.Publish(new Core.Events.MessageEvent { Module = "UI", Message = "按下:启动" });
    }
    [RelayCommand] private void Stop() => TaskStatic.Instance.StopButton = true;
    [RelayCommand] private void Pause() => TaskStatic.Instance.PauseButton = true;
    [RelayCommand] private void Reset() => TaskStatic.Instance.ResetButton = true;
    [RelayCommand] private void Estop() => TaskStatic.Instance.EstopButton = true;

    // —— 手/自动切换(互斥)——
    /// <summary>切换到自动模式:若手动时有进行中任务则尝试续跑,否则保持停止态(从头需按启动)</summary>
    [RelayCommand]
    private void SwitchToAuto()
    {
        var ts = TaskStatic.Instance;
        if (ts.WorkMode == WorkMode.Auto) return;
        ts.WorkMode = WorkMode.Auto;
        _eventBus.Publish(new Core.Events.MessageEvent { Module = "UI", Message = "切换到自动模式" });
        // 自动模式下:若有进行中任务(WorkStep!=0)则继续运行,否则保持停止(等启动)
        // 不主动改 RunStatus,让用户按启动或在 Manual→Auto 且有任务时续跑
    }

    /// <summary>切换到手动模式:停止自动流程(停轴 + 步序归零,确保手动操作时无自动动作残留)</summary>
    [RelayCommand]
    private void SwitchToManual()
    {
        var ts = TaskStatic.Instance;
        if (ts.WorkMode == WorkMode.Manual) return;
        ts.WorkMode = WorkMode.Manual;
        // 切到手动 → 停止自动流程(OnStop 会关胶阀 + 停所有轴 + 步序归零)
        if (ts.RunStatus == RunStatus.Running || ts.RunStatus == RunStatus.Paused)
            ts.RunStatus = RunStatus.Stopping;
        _eventBus.Publish(new Core.Events.MessageEvent { Module = "UI", Message = "切换到手动模式(自动流程已停止)" });
    }
}
