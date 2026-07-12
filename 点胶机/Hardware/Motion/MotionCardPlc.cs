using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;

namespace 点胶机.Hardware.Motion;

/// <summary>
/// 基于 PLC 的运动卡实现 —— 通过 IPlcHardware 读写 DB1 实现运动控制
/// 对齐 AutoStudio 的 OmronPlcDriver:上层只下发命令/读状态,实际运动学在软 PLC 内完成
/// </summary>
public sealed class MotionCardPlc : MotionCardBase
{
    public MotionCardPlc(IPlcHardware plc) : base(plc) { }

    public override void ServoOn(AxisId axis)
    {
        Plc.SetAxisServoOn(axis, true);
        Log.Information("[运动] {Axis} 伺服上电", axis);
    }

    public override void ServoOff(AxisId axis)
    {
        Plc.SetAxisServoOn(axis, false);
        Log.Information("[运动] {Axis} 伺服下电", axis);
    }

    public override bool MoveAbsolute(AxisId axis, double position, double vel)
    {
        var param = Params.TryGetValue(axis, out var p) ? p : null;
        var useVel = vel > 0 ? vel : (param?.RunSpeed ?? 100);

        // 软限位预检
        if (param != null)
        {
            if (position > param.SoftLimitPositive || position < param.SoftLimitNegative)
            {
                Log.Warning("[运动] {Axis} 目标位置 {Pos:F2} 超出软限位 [{Min:F1}, {Max:F1}]",
                    axis, position, param.SoftLimitNegative, param.SoftLimitPositive);
                return false;
            }
        }

        if (!Plc.IsAxisServoOn(axis))
        {
            Log.Warning("[运动] {Axis} 未上电,无法运动", axis);
            return false;
        }

        Plc.SetAxisMoveAbs(axis, position, useVel);
        return true;
    }

    public override bool MoveRelative(AxisId axis, double distance, double vel)
    {
        var curPos = Plc.GetAxisPosition(axis);
        return MoveAbsolute(axis, curPos + distance, vel);
    }

    public override bool JogStart(AxisId axis, int direction, double vel)
    {
        if (!Plc.IsAxisServoOn(axis)) return false;
        var param = Params.TryGetValue(axis, out var p) ? p : null;
        var useVel = vel > 0 ? vel : (param?.RunSpeed ?? 100);
        Plc.SetAxisJog(axis, direction, useVel);
        return true;
    }

    public override void JogStop(AxisId axis) => Plc.SetAxisJog(axis, 0, 0);

    public override bool Home(AxisId axis)
    {
        if (!Plc.IsAxisServoOn(axis)) return false;
        var param = Params.TryGetValue(axis, out var p) ? p : null;
        Plc.StartHome(axis, param?.HomeSpeed ?? 20);
        Log.Information("[运动] {Axis} 开始回零", axis);
        return true;
    }

    public override void Stop(AxisId axis) => Plc.StopAxis(axis);
}
