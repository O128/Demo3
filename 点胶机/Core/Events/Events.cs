using 点胶机.Core.Enums;

namespace 点胶机.Core.Events;

/// <summary>
/// 通用消息事件(日志/通知,对齐 AutoStudio 的 MessageEvent)
/// </summary>
public class MessageEvent
{
    public DateTime DateTime { get; set; } = DateTime.Now;
    public string Module { get; set; } = "";
    public string Message { get; set; } = "";
    public NotifyType NotifyType { get; set; } = NotifyType.NotifyList;
}

/// <summary>
/// 报警事件 —— 由报警服务发布,UI(AlarmView)订阅
/// </summary>
public class AlarmEvent
{
    public int AlarmId { get; set; }
    public string Name { get; set; } = "";
    public AlarmLevel Level { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public bool IsActive { get; set; }   // true=新报警触发,false=已确认消除
    public string? Message { get; set; }
}

/// <summary>
/// 设备主状态变化事件 —— UI 顶部状态条订阅
/// </summary>
public class StatusChangedEvent
{
    public RunStatus RunStatus { get; set; }
    public ReadyStatus ReadyStatus { get; set; }
    public WorkMode WorkMode { get; set; }
}
