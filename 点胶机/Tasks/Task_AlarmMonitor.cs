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

    /// <summary>闪烁计时(每 ~500ms 翻转一次)</summary>
    private double _blinkAccumMs;
    private bool _blinkOn;

    /// <summary>根据设备状态联动三色灯和蜂鸣器
    /// 规则:急停=红;手动模式=黄;自动运行=绿;自动暂停=闪烁绿;停止=黄绿同亮;
    ///       蜂鸣器在有报警(停机/暂停级)时闪烁</summary>
    private void UpdateLamps()
    {
        // 闪烁计时累加(本任务被调度周期约 1ms,但报警时每周期都跑,累加近似)
        _blinkAccumMs += 5;
        if (_blinkAccumMs >= 500) { _blinkAccumMs = 0; _blinkOn = !_blinkOn; }

        var ts = TaskStatic.Instance;
        var active = _alarmSvc.GetActiveAlarms();
        bool hasStop = active.Exists(a => a.Level == AlarmLevel.Alarm_Stop);
        bool hasPause = active.Exists(a => a.Level == AlarmLevel.Alarm_Pause);
        bool hasAnyAlarm = hasStop || hasPause;

        // 默认熄灭
        bool red = false, yellow = false, green = false, buzzer = false;

        if (hasStop)
        {
            // 停机级报警(含急停、限位、伺服故障)= 红
            red = true;
            buzzer = _blinkOn;   // 蜂鸣闪烁
        }
        else if (hasPause)
        {
            // 暂停级报警(气压等)= 黄 + 蜂鸣闪烁
            yellow = true;
            buzzer = _blinkOn;
        }
        else
        {
            // 无报警:按运行状态
            switch (ts.RunStatus)
            {
                case RunStatus.Running:
                    green = true;          // 自动运行 = 绿
                    break;
                case RunStatus.Paused:
                    green = _blinkOn;      // 自动暂停 = 闪烁绿
                    break;
                case RunStatus.Stopping:
                    // 停止态:黄绿同亮
                    yellow = true;
                    green = true;
                    break;
            }

            // 手动模式下:覆盖为黄灯(无论运行状态)
            if (ts.WorkMode == WorkMode.Manual)
            {
                yellow = true;
                green = false;
            }
        }

        _plc.WriteOutput(IoIndex.Out_RedLamp, red);
        _plc.WriteOutput(IoIndex.Out_YellowLamp, yellow);
        _plc.WriteOutput(IoIndex.Out_GreenLamp, green);
        _plc.WriteOutput(IoIndex.Out_Buzzer, buzzer);
    }

    protected override void AlwaysRun(WorkMode mode)
    {
        // 持续累积胶阀胶量(即使非 Running 也要 Tick?不,仅在点胶时开阀才累计,这里不处理)
    }
}
