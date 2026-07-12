using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;

namespace 点胶机.Hardware.Motion;

/// <summary>
/// 运动卡基类 —— 提供 IMotionCard 的默认实现骨架
/// </summary>
public abstract class MotionCardBase : IMotionCard
{
    protected readonly IPlcHardware Plc;
    protected readonly Dictionary<AxisId, AxisParameter> Params = new();

    protected MotionCardBase(IPlcHardware plc)
    {
        Plc = plc;
    }

    public virtual void ApplyAxisParameter(AxisId axis, AxisParameter param)
    {
        Params[axis] = param;
    }

    public virtual void ClearFault(AxisId axis) => Plc.ClearAxisFault(axis);

    public virtual bool GetPosition(out double pos, AxisId axis)
    {
        pos = Plc.GetAxisPosition(axis);
        return true;
    }

    public double GetPosition(AxisId axis) => Plc.GetAxisPosition(axis);
    public double GetVelocity(AxisId axis) => Plc.GetAxisVelocity(axis);
    public bool IsInPosition(AxisId axis) => Plc.IsAxisInPosition(axis);
    public bool IsMoving(AxisId axis) => Plc.IsAxisMoving(axis);
    public bool IsServoOn(AxisId axis) => Plc.IsAxisServoOn(axis);
    public bool IsFault(AxisId axis) => Plc.IsAxisFault(axis);
    public bool IsHomed(AxisId axis) => Plc.IsAxisHomed(axis);

    public abstract void ServoOn(AxisId axis);
    public abstract void ServoOff(AxisId axis);
    public abstract bool MoveAbsolute(AxisId axis, double position, double vel);
    public abstract bool MoveRelative(AxisId axis, double distance, double vel);
    public abstract bool JogStart(AxisId axis, int direction, double vel);
    public abstract void JogStop(AxisId axis);
    public abstract bool Home(AxisId axis);
    public abstract void Stop(AxisId axis);
}
