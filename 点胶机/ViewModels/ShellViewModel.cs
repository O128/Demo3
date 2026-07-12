using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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
    [ObservableProperty] private string _version = "V1.0.0";
    [ObservableProperty] private string _runStatusText = "停止";
    [ObservableProperty] private string _runStatusColor = "Gray";
    [ObservableProperty] private string _workModeText = "Auto";
    [ObservableProperty] private string _readyStatusText = "初始化中...";

    public ShellViewModel(IServiceProvider services, IEventBus eventBus)
    {
        _services = services;
        _eventBus = eventBus;
        CurrentView = services.GetRequiredService<HomeViewModel>();

        // 订阅状态变化事件,更新顶部状态条
        _eventBus.Subscribe<Core.Events.StatusChangedEvent>(OnStatusChanged);
        // 初始读取一次
        RefreshStatus();
    }

    private void OnStatusChanged(Core.Events.StatusChangedEvent e)
    {
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var ts = TaskStatic.Instance;
        RunStatusText = ts.RunStatus switch
        {
            Core.Enums.RunStatus.Running => "运行中",
            Core.Enums.RunStatus.Paused => "暂停",
            _ => "停止"
        };
        RunStatusColor = ts.RunStatus switch
        {
            Core.Enums.RunStatus.Running => "Green",
            Core.Enums.RunStatus.Paused => "Orange",
            _ => "Gray"
        };
        WorkModeText = ts.WorkMode.ToString();
        ReadyStatusText = ts.ReadyStatus switch
        {
            Core.Enums.ReadyStatus.Initialized => "就绪",
            Core.Enums.ReadyStatus.Initializing => "初始化中...",
            _ => "未初始化"
        };
    }

    // —— 导航命令 ——
    [RelayCommand] private void GoHome() => CurrentView = _services.GetRequiredService<HomeViewModel>();
    [RelayCommand] private void GoManual() => CurrentView = _services.GetRequiredService<ManualViewModel>();
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
}
