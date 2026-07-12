using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Engine;
using 点胶机.Hardware;
using 点胶机.Hardware.Devices;
using 点胶机.Hardware.Plc;

namespace 点胶机.Tasks;

/// <summary>
/// IO 看门狗任务 —— 把软 PLC 输入(传感器)同步到 TaskStatic 按钮信号
/// 仿真场景:外部可用 SetInput 模拟按钮;本任务读取后驱动 TaskStatic.ProcessButtons
/// 同时周期 Tick 胶阀累计胶量
/// </summary>
public sealed class Task_IoWatchdog : TaskBase
{
    private readonly HardwareManager _hw;
    private readonly GlueValveSimulator _glue;

    public Task_IoWatchdog(HardwareManager hw, GlueValveSimulator glue)
        : base("IO 看门狗")
    {
        _hw = hw;
        _glue = glue;
        IgnoreAlarmPause = true;   // 看门狗始终运行
    }

    protected override void AutoRun(WorkMode mode)
    {
        // 空实现:主要逻辑在 AlwaysRun
    }

    protected override void AlwaysRun(WorkMode mode)
    {
        var plc = _hw.Plc;
        var ts = TaskStatic.Instance;

        // —— 同步软 PLC 输入到按钮信号 ——
        // (真实场景按钮接 PLC 输入;UI 按钮也直接写 TaskStatic,两条路径合一)
        if (plc.ReadInput(IoIndex.In_Estop)) ts.EstopButton = true;
        if (plc.ReadInput(IoIndex.In_StartButton)) ts.StartButton = true;
        if (plc.ReadInput(IoIndex.In_StopButton)) ts.StopButton = true;
        if (plc.ReadInput(IoIndex.In_PauseButton)) ts.PauseButton = true;
        if (plc.ReadInput(IoIndex.In_ResetButton)) ts.ResetButton = true;

        // —— 处理按钮信号 → 切换运行状态 ——
        ts.ProcessButtons();

        // —— 胶阀胶量 Tick(按调度周期近似)——
        _glue.Tick(1);
    }
}
