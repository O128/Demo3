using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Engine;
using 点胶机.Hardware;
using 点胶机.Hardware.Plc;

namespace 点胶机.Tasks;

/// <summary>
/// 报警监听任务 —— 每周期扫描软 PLC 报警字 + 传感器条件,触发报警
/// IgnoreAlarmPause = true:报警时本任务仍执行(否则无法检测报警解除)
/// 对齐 AutoStudio.Service.AlarmService.AlarmListen
/// </summary>
public sealed class Task_AlarmMonitor : TaskBase
{
    private readonly IAlarmService _alarmSvc;
    private readonly HardwareManager _hw;
    private readonly IPressureSensor _pressure;
    private readonly PlcSimulator _plc;

    public Task_AlarmMonitor(IAlarmService alarmSvc, HardwareManager hw, IPressureSensor pressure)
        : base("报警监听")
    {
        _alarmSvc = alarmSvc;
        _hw = hw;
        _pressure = pressure;
        _plc = hw.Plc;
        IgnoreAlarmPause = true;   // 关键:报警时本任务继续运行
    }

    protected override void AutoRun(WorkMode mode)
    {
        var db = _plc.DataBlock;

        // —— 急停(DB5.AlarmBit.Estop)——
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.Estop], 0, AlarmLevel.Alarm_Stop, "急停触发");

        // —— 轴限位 ——
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.X_LimitPos], 1, AlarmLevel.Alarm_Stop, "X 轴正限位");
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.X_LimitNeg], 2, AlarmLevel.Alarm_Stop, "X 轴负限位");
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.Y_LimitPos], 3, AlarmLevel.Alarm_Stop, "Y 轴正限位");
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.Y_LimitNeg], 4, AlarmLevel.Alarm_Stop, "Y 轴负限位");
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.Z_LimitPos], 5, AlarmLevel.Alarm_Stop, "Z 轴正限位");
        _alarmSvc.AddAlarm(db.AlarmWord[S7DataBlock.AlarmBit.Z_LimitNeg], 6, AlarmLevel.Alarm_Stop, "Z 轴负限位");

        // —— 伺服故障 ——
        bool anyFault = _plc.IsAxisFault(AxisId.X) || _plc.IsAxisFault(AxisId.Y) || _plc.IsAxisFault(AxisId.Z);
        _alarmSvc.AddAlarm(anyFault, 7, AlarmLevel.Alarm_Stop, "伺服驱动器故障");

        // —— 气压低(<300kpa 视为不足)——
        var pressure = _pressure.GetValue();
        _alarmSvc.AddAlarm(pressure < 300, 9, AlarmLevel.Alarm_Pause, $"气压不足({pressure:F0}kpa)");

        // —— 联动三色灯/蜂鸣(写 DB2.Output)——
        UpdateLamps();
    }

    /// <summary>根据报警状态联动三色灯和蜂鸣器</summary>
    private void UpdateLamps()
    {
        var active = _alarmSvc.GetActiveAlarms();
        bool hasStop = active.Exists(a => a.Level == AlarmLevel.Alarm_Stop);
        bool hasPause = active.Exists(a => a.Level == AlarmLevel.Alarm_Pause);

        // 红灯:停机报警;黄灯:暂停报警;绿灯:正常运行
        _plc.WriteOutput(IoIndex.Out_RedLamp, hasStop);
        _plc.WriteOutput(IoIndex.Out_YellowLamp, hasPause && !hasStop);
        _plc.WriteOutput(IoIndex.Out_GreenLamp, !hasStop && !hasPause
            && TaskStatic.Instance.RunStatus == RunStatus.Running);
        // 蜂鸣:停机/暂停报警时响
        _plc.WriteOutput(IoIndex.Out_Buzzer, hasStop || hasPause);
    }

    protected override void AlwaysRun(WorkMode mode)
    {
        // 持续累积胶阀胶量(即使非 Running 也要 Tick?不,仅在点胶时开阀才累计,这里不处理)
    }
}
