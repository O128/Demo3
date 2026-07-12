namespace 点胶机.Core.Enums;

/// <summary>
/// 设备主运行状态(全局调度依据)
/// </summary>
public enum RunStatus
{
    /// <summary>停机/未启动</summary>
    Stopping = 0,
    /// <summary>运行中</summary>
    Running = 1,
    /// <summary>暂停</summary>
    Paused = 2
}

/// <summary>
/// 设备就绪状态(初始化生命周期)
/// </summary>
public enum ReadyStatus
{
    Uninitialized = 0,
    Initializing = 1,
    Initialized = 2
}

/// <summary>
/// 工作模式
/// </summary>
public enum WorkMode
{
    /// <summary>手动模式</summary>
    Manual = 0,
    /// <summary>自动模式</summary>
    Auto = 1,
    /// <summary>空跑模式(跳过到位/IO 等待,纯走逻辑)</summary>
    EmptyRun = 2
}

/// <summary>
/// 报警级别
/// </summary>
public enum AlarmLevel
{
    /// <summary>提示(不停机)</summary>
    Tip = 0,
    /// <summary>暂停级报警</summary>
    Alarm_Pause = 1,
    /// <summary>停机级报警(最高)</summary>
    Alarm_Stop = 2
}

/// <summary>
/// 通知类型(事件总线用)
/// </summary>
[Flags]
public enum NotifyType
{
    None = 0,
    /// <summary>加入报警/日志列表</summary>
    NotifyList = 1,
    /// <summary>弹窗</summary>
    DialogNotify = 2,
    /// <summary>NG 提示样式</summary>
    NgNotify = 4
}
