using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Engine;
using 点胶机.Hardware;

namespace 点胶机.Tasks;

/// <summary>
/// 系统初始化任务 —— 伺服上电 + 各轴回零 + 置就绪状态
/// 对齐 AutoStudio.Task_系统初始化(IsInitTask=true)
/// 步序:0→10 伺服上电 → 20/21/22 三轴回零 → 100 就绪
/// </summary>
public sealed class Task_SystemInit : TaskBase
{
    public override bool IsInitTask => true;

    private readonly HardwareManager _hw;

    public Task_SystemInit(HardwareManager hw) : base("系统初始化")
    {
        _hw = hw;
    }

    protected override void AutoRun(WorkMode mode)
    {
        var ts = TaskStatic.Instance;

        // 已就绪则不再执行初始化(Initializing/Uninitialized 时继续推进)
        if (ts.ReadyStatus == ReadyStatus.Initialized) return;

        if (ts.ReadyStatus == ReadyStatus.Uninitialized)
        {
            ts.ReadyStatus = ReadyStatus.Initializing;
        }

        switch (WorkStep)
        {
            case 0:
                SetStep(10, "伺服上电");
                break;

            case 10:
                _hw.ServoOnAll();
                if (Timer.Start(1000))
                {
                    SetStep(20, "X 轴回零");
                }
                break;

            case 20:
                _hw.Motion.Home(AxisId.X);
                SetStep(21, "X 轴回零中");
                break;
            case 21:
                if (_hw.Motion.IsHomed(AxisId.X))
                    SetStep(22, "Y 轴回零");
                else if (Timer.Start(15000))
                    OnTimeout("X 轴回零超时");
                break;
            case 22:
                _hw.Motion.Home(AxisId.Y);
                SetStep(23, "Y 轴回零中");
                break;
            case 23:
                if (_hw.Motion.IsHomed(AxisId.Y))
                    SetStep(24, "Z 轴回零");
                else if (Timer.Start(15000))
                    OnTimeout("Y 轴回零超时");
                break;
            case 24:
                _hw.Motion.Home(AxisId.Z);
                SetStep(25, "Z 轴回零中");
                break;
            case 25:
                if (_hw.Motion.IsHomed(AxisId.Z))
                    SetStep(100, "初始化完成");
                else if (Timer.Start(15000))
                    OnTimeout("Z 轴回零超时");
                break;

            case 100:
                ts.ReadyStatus = ReadyStatus.Initialized;
                // 默认进入停止态,等用户按启动
                ts.RunStatus = RunStatus.Stopping;
                Log.Information("===== 系统初始化完成,等待启动 =====");
                // 初始化完成后此任务不再执行(ReadyStatus 已不是 Uninitialized)
                break;

            case 1000:
                // 异常态:等复位
                if (Timer.Start(2000)) SetStep(0, "重新初始化");
                break;
        }
    }

    private void OnTimeout(string desc)
    {
        Log.Error("[初始化] {Desc}", desc);
        WorkStep = 1000;
        StepDesc = desc;
    }
}
