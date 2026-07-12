using 点胶机.Core.Enums;
using 点胶机.Core.Events;

namespace 点胶机.Core.Interfaces;

/// <summary>
/// 事件总线接口 —— Prism 风格的发布/订阅,解耦 UI 与业务
/// </summary>
public interface IEventBus
{
    /// <summary>订阅指定事件</summary>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    /// <summary>发布事件</summary>
    void Publish<TEvent>(TEvent @event) where TEvent : notnull;

    /// <summary>取消订阅</summary>
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;
}

/// <summary>
/// 对话框服务接口 —— 负责报警弹窗 / 提示 / 确认框(NotifyWindow 的抽象)
/// </summary>
public interface IDialogService
{
    /// <summary>报警弹窗(模态,带确认按钮)</summary>
    void ShowAlarm(int alarmId, string name, AlarmLevel level, string message);

    /// <summary>提示(自动消失)</summary>
    void ShowTip(string message);

    /// <summary>错误弹窗(模态)</summary>
    void ShowError(string message);

    /// <summary>确认对话框,返回是否确认</summary>
    bool ShowConfirm(string message);
}

/// <summary>
/// 报警服务接口
/// </summary>
public interface IAlarmService
{
    /// <summary>当前是否存在未确认报警</summary>
    bool HasActiveAlarm { get; }

    /// <summary>添加/触发一条报警(条件为 true 时生效)</summary>
    void AddAlarm(bool condition, int alarmId, AlarmLevel level, string name);

    /// <summary>确认(消除)当前报警</summary>
    void Ack();

    /// <summary>订阅报警变化</summary>
    event Action<AlarmRecord>? AlarmChanged;

    /// <summary>获取当前活跃报警列表(UI 用)</summary>
    List<AlarmRecord> GetActiveAlarms();
}

/// <summary>
/// 一条报警记录(流转用)
/// </summary>
public record AlarmRecord(
    int AlarmId,
    string Name,
    AlarmLevel Level,
    DateTime StartTime,
    string? Message = null);

/// <summary>
/// 软 PLC 硬件接口 —— 屏蔽 Sim / 真机(S7Net)差异
/// </summary>
public interface IPlcHardware
{
    bool IsConnected { get; }

    // —— IO(对应 DB2)——
    bool ReadInput(int index);
    bool ReadOutput(int index);
    void WriteOutput(int index, bool value);

    // —— 轴控制(对应 DB1)——
    void SetAxisServoOn(AxisId axis, bool on);
    void SetAxisMoveAbs(AxisId axis, double targetPos, double vel);
    void SetAxisMoveRel(AxisId axis, double distance, double vel);
    void SetAxisJog(AxisId axis, int direction, double vel);
    void StopAxis(AxisId axis);
    void StartHome(AxisId axis, double homeSpeed);
    void ClearAxisFault(AxisId axis);

    // —— 轴状态(对应 DB1)——
    double GetAxisPosition(AxisId axis);
    double GetAxisVelocity(AxisId axis);
    bool IsAxisInPosition(AxisId axis);
    bool IsAxisMoving(AxisId axis);
    bool IsAxisServoOn(AxisId axis);
    bool IsAxisFault(AxisId axis);
    bool IsAxisHomed(AxisId axis);
    bool IsAxisLimitPositive(AxisId axis);
    bool IsAxisLimitNegative(AxisId axis);

    // —— 主状态/命令/报警(对应 DB3/DB4/DB5)——
    void SetMainCommand(int cmdIndex, bool value);
    bool GetMainStatus(int statusIndex);
    void SetAlarmWord(int index, bool value);
    bool GetAlarmWord(int index);
}

/// <summary>
/// 运动卡接口 —— 封装 IPlcHardware 之上,提供工程级运动语义
/// </summary>
public interface IMotionCard
{
    void ServoOn(AxisId axis);
    void ServoOff(AxisId axis);
    bool MoveAbsolute(AxisId axis, double position, double vel);
    bool MoveRelative(AxisId axis, double distance, double vel);
    bool JogStart(AxisId axis, int direction, double vel);
    void JogStop(AxisId axis);
    bool Home(AxisId axis);
    void Stop(AxisId axis);
    void ClearFault(AxisId axis);

    double GetPosition(AxisId axis);
    double GetVelocity(AxisId axis);
    bool IsInPosition(AxisId axis);
    bool IsMoving(AxisId axis);
    bool IsServoOn(AxisId axis);
    bool IsFault(AxisId axis);
    bool IsHomed(AxisId axis);

    /// <summary>设置软限位/速度等轴参数</summary>
    void ApplyAxisParameter(AxisId axis, AxisParameter param);
}

/// <summary>轴参数</summary>
public class AxisParameter
{
    public AxisId Axis { get; set; }
    public double PulseUnit { get; set; } = 1000;       // 脉冲当量(脉冲/mm)
    public double SoftLimitPositive { get; set; } = 300;
    public double SoftLimitNegative { get; set; } = -10;
    public double RunSpeed { get; set; } = 100;
    public double HomeSpeed { get; set; } = 20;
}

/// <summary>
/// 点胶阀接口
/// </summary>
public interface IGlueValve
{
    void Open();
    void Close();
    bool IsOpen { get; }
    /// <summary>累计出胶量(已开阀总时长 × 流量)</summary>
    double AccumulatedAmount { get; }
    void ResetAccumulator();
}

/// <summary>
/// 压力传感器接口(用于气压报警监听)
/// </summary>
public interface IPressureSensor
{
    double GetValue();
}
