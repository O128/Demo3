using System.ComponentModel;
using System.Runtime.CompilerServices;
using 点胶机.Core.Enums;
using 点胶机.Core.Events;
using 点胶机.Core.Interfaces;

namespace 点胶机.Engine;

/// <summary>
/// 全局任务状态单例 —— UI/任务/硬件共享,对齐 AutoStudio.TaskStatic
/// UI 按钮直接写这些属性触发流程;任务调度器依据 RunStatus 分派
/// </summary>
public sealed class TaskStatic : INotifyPropertyChanged
{
    private static readonly Lazy<TaskStatic> _instance = new(() => new TaskStatic());
    public static TaskStatic Instance => _instance.Value;

    private RunStatus _runStatus = RunStatus.Stopping;
    private ReadyStatus _readyStatus = ReadyStatus.Uninitialized;
    private WorkMode _workMode = WorkMode.Auto;
    private bool _isAlarm;
    private bool _isTip;

    // —— 按钮信号(UI 写,任务读)——
    public bool StartButton { get; set; }
    public bool StopButton { get; set; }
    public bool PauseButton { get; set; }
    public bool ResetButton { get; set; }
    public bool EstopButton { get; set; }

    // —— 统计 ——
    private int _todayYield;
    private int _todayAlarmCount;
    private DateTime _bootTime = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;

    private TaskStatic() { }

    /// <summary>运行状态(Stopping/Running/Paused)</summary>
    public RunStatus RunStatus
    {
        get => _runStatus;
        set { if (_runStatus != value) { _runStatus = value; OnChanged(); PublishStatus(); } }
    }

    /// <summary>就绪状态</summary>
    public ReadyStatus ReadyStatus
    {
        get => _readyStatus;
        set { if (_readyStatus != value) { _readyStatus = value; OnChanged(); PublishStatus(); } }
    }

    /// <summary>工作模式</summary>
    public WorkMode WorkMode
    {
        get => _workMode;
        set { if (_workMode != value) { _workMode = value; OnChanged(); PublishStatus(); } }
    }

    /// <summary>当前是否存在未确认的停机/暂停级报警</summary>
    public bool IsAlarm
    {
        get => _isAlarm;
        set { if (_isAlarm != value) { _isAlarm = value; OnChanged(); } }
    }

    /// <summary>当前是否有提示(非阻塞)</summary>
    public bool IsTip
    {
        get => _isTip;
        set { if (_isTip != value) { _isTip = value; OnChanged(); } }
    }

    /// <summary>今日产量</summary>
    public int TodayYield
    {
        get => _todayYield;
        set { if (_todayYield != value) { _todayYield = value; OnChanged(); } }
    }

    /// <summary>今日报警数</summary>
    public int TodayAlarmCount
    {
        get => _todayAlarmCount;
        set { if (_todayAlarmCount != value) { _todayAlarmCount = value; OnChanged(); } }
    }

    /// <summary>开机时间(用于 OEE 运行时长)</summary>
    public DateTime BootTime
    {
        get => _bootTime;
        set { _bootTime = value; OnChanged(); }
    }

    /// <summary>运行时长(秒)</summary>
    public double RunSeconds => (DateTime.Now - _bootTime).TotalSeconds;

    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ===== 按钮处理(由 Task_IoWatchdog 调用)=====
    /// <summary>处理 UI 按钮信号 → 切换运行状态</summary>
    public void ProcessButtons()
    {
        // 急停最高优先级
        if (EstopButton)
        {
            RunStatus = RunStatus.Stopping;
            EstopButton = false;
        }
        if (ResetButton)
        {
            ResetButton = false;
            IsAlarm = false;
            RunStatus = RunStatus.Stopping;
            ReadyStatus = ReadyStatus.Initialized; // 复位后保留就绪
        }
        if (StopButton)
        {
            StopButton = false;
            RunStatus = RunStatus.Stopping;
        }
        if (PauseButton)
        {
            PauseButton = false;
            if (RunStatus == RunStatus.Running)
                RunStatus = RunStatus.Paused;
        }
        if (StartButton)
        {
            StartButton = false;
            if (ReadyStatus == ReadyStatus.Initialized && !IsAlarm)
                RunStatus = RunStatus.Running;
        }
    }

    // ===== 事件总线发布 =====
    public IEventBus? EventBus { get; set; }

    private void PublishStatus()
    {
        EventBus?.Publish(new StatusChangedEvent
        {
            RunStatus = _runStatus,
            ReadyStatus = _readyStatus,
            WorkMode = _workMode
        });
    }
}
