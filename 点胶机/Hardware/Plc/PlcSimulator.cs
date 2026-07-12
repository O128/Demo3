using System.Diagnostics;
using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;

namespace 点胶机.Hardware.Plc;

/// <summary>
/// 西门子 S7-1500 软 PLC 仿真器 —— 自建内存仿真,不走真实 S7 协议
/// 后台扫描线程(1ms 周期)模拟 PLC 扫描周期:
///   1) 执行轴运动学(积分位置,到目标置 InPos)
///   2) 检测软限位/急停 → 置报警位
///   3) 同步 IO/主状态字
/// 实现 IPlcHardware;将来换真机时只需新增 PlcS7Net 实现同一接口
/// </summary>
public sealed class PlcSimulator : IPlcHardware, IDisposable
{
    private readonly S7DataBlock _db = new();
    private Thread? _scanThread;
    private volatile bool _running;
    private readonly int _scanCycleMs;
    private readonly Stopwatch _sw = new();
    private double _lastElapsedSec;

    public bool IsConnected { get; private set; }

    public S7DataBlock DataBlock => _db;

    public PlcSimulator(int scanCycleMs = 1)
    {
        _scanCycleMs = Math.Max(1, scanCycleMs);
        // 急停按钮为常绿(高电平有效表示"正常未按下"),初始未触发=true
        // 按下急停 → 输入变 false(低电平)→ 检测 !In_Estop 触发报警
        _db.Inputs[IoIndex.In_Estop] = true;
        // 启动/停止/暂停/复位 按钮常态为 false(按下=true)
    }

    /// <summary>启动扫描线程</summary>
    public void Start()
    {
        if (_running) return;
        _running = true;
        IsConnected = true;
        _sw.Start();
        _scanThread = new Thread(ScanLoop)
        {
            IsBackground = true,
            Name = "PlcScan"
        };
        _scanThread.Start();
        Log.Information("[软PLC] 扫描线程已启动,周期 {Ms}ms", _scanCycleMs);
    }

    // ============ IPlcHardware:IO(DB2)============
    public bool ReadInput(int index) =>
        index >= 0 && index < _db.Inputs.Length && _db.Inputs[index];

    public bool ReadOutput(int index) =>
        index >= 0 && index < _db.Outputs.Length && _db.Outputs[index];

    public void WriteOutput(int index, bool value)
    {
        if (index >= 0 && index < _db.Outputs.Length)
            _db.Outputs[index] = value;
    }

    // ============ IPlcHardware:轴控制(DB1)============
    public void SetAxisServoOn(AxisId axis, bool on)
    {
        var a = _db.Axes[(int)axis];
        a.ServoOnCmd = on;
    }

    public void SetAxisMoveAbs(AxisId axis, double targetPos, double vel)
    {
        var a = _db.Axes[(int)axis];
        a.TargetPos = targetPos;
        a.MoveVel = Math.Abs(vel);
        a.MoveAbsCmd = true;
        a.MoveRelCmd = false;
    }

    public void SetAxisMoveRel(AxisId axis, double distance, double vel)
    {
        var a = _db.Axes[(int)axis];
        a.TargetPos = a.ActPos + distance;
        a.MoveVel = Math.Abs(vel);
        a.MoveAbsCmd = true;   // 内部转绝对定位
        a.MoveRelCmd = false;
    }

    public void SetAxisJog(AxisId axis, int direction, double vel)
    {
        var a = _db.Axes[(int)axis];
        a.JogVel = Math.Abs(vel);
        if (direction > 0) { a.JogFwdCmd = true; a.JogBwdCmd = false; }
        else if (direction < 0) { a.JogFwdCmd = false; a.JogBwdCmd = true; }
        else { a.JogFwdCmd = false; a.JogBwdCmd = false; }
    }

    public void StopAxis(AxisId axis)
    {
        var a = _db.Axes[(int)axis];
        a.StopCmd = true;
        a.MoveAbsCmd = false;
        a.JogFwdCmd = false;
        a.JogBwdCmd = false;
    }

    public void StartHome(AxisId axis, double homeSpeed)
    {
        var a = _db.Axes[(int)axis];
        a.HomeVel = homeSpeed;
        a.HomeCmd = true;
    }

    public void ClearAxisFault(AxisId axis)
    {
        var a = _db.Axes[(int)axis];
        a.ResetCmd = true;
    }

    // ============ IPlcHardware:轴状态(DB1)============
    public double GetAxisPosition(AxisId axis) => _db.Axes[(int)axis].ActPos;
    public double GetAxisVelocity(AxisId axis) => _db.Axes[(int)axis].ActVel;
    public bool IsAxisInPosition(AxisId axis) => _db.Axes[(int)axis].InPos;
    public bool IsAxisMoving(AxisId axis) => _db.Axes[(int)axis].Moving;
    public bool IsAxisServoOn(AxisId axis) => _db.Axes[(int)axis].ServoOn;
    public bool IsAxisFault(AxisId axis) => _db.Axes[(int)axis].Fault;
    public bool IsAxisHomed(AxisId axis) => _db.Axes[(int)axis].Homed;
    public bool IsAxisLimitPositive(AxisId axis) => _db.Axes[(int)axis].LimitPos;
    public bool IsAxisLimitNegative(AxisId axis) => _db.Axes[(int)axis].LimitNeg;

    // ============ IPlcHardware:主状态/命令/报警(DB3/4/5)============
    public void SetMainCommand(int cmdIndex, bool value)
    {
        if (cmdIndex >= 0 && cmdIndex < _db.MainCommand.Length)
            _db.MainCommand[cmdIndex] = value;
    }

    public bool GetMainStatus(int statusIndex) =>
        statusIndex >= 0 && statusIndex < _db.MainStatus.Length && _db.MainStatus[statusIndex];

    public void SetAlarmWord(int index, bool value)
    {
        if (index >= 0 && index < _db.AlarmWord.Length)
            _db.AlarmWord[index] = value;
    }

    public bool GetAlarmWord(int index) =>
        index >= 0 && index < _db.AlarmWord.Length && _db.AlarmWord[index];

    /// <summary>允许外部写输入(模拟传感器信号,如急停按钮、有工件)</summary>
    public void SetInput(int index, bool value)
    {
        if (index >= 0 && index < _db.Inputs.Length)
            _db.Inputs[index] = value;
    }

    // ============ 扫描循环(模拟 PLC 程序)============
    private void ScanLoop()
    {
        while (_running)
        {
            try
            {
                var nowSec = _sw.Elapsed.TotalSeconds;
                var dt = nowSec - _lastElapsedSec;
                _lastElapsedSec = nowSec;

                // —— 1) 急停检测(低电平有效:按下=false 触发)——
                if (!_db.Inputs[IoIndex.In_Estop])
                {
                    _db.AlarmWord[S7DataBlock.AlarmBit.Estop] = true;
                    _db.MainStatus[S7DataBlock.MainStatusBit.AlarmA] = true;
                    // 急停:所有轴立即停止
                    foreach (var a in _db.Axes)
                    {
                        a.Moving = false;
                        a.MoveAbsCmd = false;
                        a.JogFwdCmd = false;
                        a.JogBwdCmd = false;
                    }
                }

                // —— 2) 轴运动学(每轴独立计算)——
                foreach (var a in _db.Axes)
                {
                    ProcessAxis(a, dt);
                }

                // —— 3) 软限位检测 → 报警 ——
                CheckSoftLimits();

                // —— 4) 同步主状态(由上位机设置的命令位反映到状态)——
                // 这里只做最小同步,主状态主要由上位机通过命令控制
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[软PLC] 扫描循环异常");
            }

            Thread.Sleep(_scanCycleMs);
        }
    }

    /// <summary>单轴运动学处理(扫描周期内调用)</summary>
    private void ProcessAxis(AxisBlock a, double dt)
    {
        // 复位命令处理
        if (a.ResetCmd)
        {
            a.Fault = false;
            a.ResetCmd = false;
        }

        // 伺服上电命令
        if (a.ServoOnCmd && !a.ServoOn)
        {
            a.ServoOn = true;
            Log.Information("[软PLC] {Axis} 伺服上电", a.Axis);
        }
        if (!a.ServoOnCmd && a.ServoOn)
        {
            a.ServoOn = false;
            a.Moving = false;
        }

        // 未上电不运动
        if (!a.ServoOn)
        {
            a.ActVel = 0;
            return;
        }

        // 停止命令
        if (a.StopCmd)
        {
            a.StopCmd = false;
            a.Moving = false;
            a.MoveAbsCmd = false;
            a.JogFwdCmd = false;
            a.JogBwdCmd = false;
            a.ActVel = 0;
            a.InPos = true;
        }

        // 回零
        if (a.HomeCmd)
        {
            if (!a.Moving)
            {
                a._moveStartPos = a.ActPos;
                a._moveDir = a.ActPos > 0 ? -1 : 0;   // 向原点方向
                a.Moving = true;
                a.InPos = false;
                a.ActVel = a.HomeVel * a._moveDir;
            }
            // 朝 0 移动
            if (a._moveDir < 0 && a.ActPos > 0)
            {
                a.ActPos -= a.HomeVel * dt;
                if (a.ActPos <= 0)
                {
                    a.ActPos = 0;
                    FinishMove(a, homed: true);
                }
            }
            else if (a._moveDir == 0)
            {
                // 已在原点
                FinishMove(a, homed: true);
            }
            return;
        }

        // 点动
        if (a.JogFwdCmd || a.JogBwdCmd)
        {
            var dir = a.JogFwdCmd ? 1 : -1;
            a.ActPos += a.JogVel * dir * dt;
            a.ActVel = a.JogVel * dir;
            a.Moving = true;
            a.InPos = false;
            a.MoveAbsCmd = false;   // 点动取消定位
            return;
        }
        else if (a.Moving && a.ActVel != 0 && !a.MoveAbsCmd)
        {
            // 释放点动 → 停止
            a.Moving = false;
            a.ActVel = 0;
            a.InPos = true;
        }

        // 绝对定位
        if (a.MoveAbsCmd)
        {
            if (!a.Moving)
            {
                // 启动新运动
                a._moveStartPos = a.ActPos;
                a._moveDir = Math.Sign(a.TargetPos - a.ActPos);
                a.Moving = true;
                a.InPos = false;
                a.MoveAbsCmd = false;   // 消费命令
                a.ActVel = a.MoveVel * a._moveDir;
            }

            if (a._moveDir != 0)
            {
                a.ActPos += a.MoveVel * a._moveDir * dt;
                // 判断是否越过目标
                var remaining = a.TargetPos - a.ActPos;
                if ((a._moveDir > 0 && remaining <= 0) || (a._moveDir < 0 && remaining >= 0))
                {
                    a.ActPos = a.TargetPos;
                    FinishMove(a, homed: a.Homed);
                }
            }
            else
            {
                // 已在目标位置
                FinishMove(a, homed: a.Homed);
            }
        }
    }

    private static void FinishMove(AxisBlock a, bool homed)
    {
        a.Moving = false;
        a.InPos = true;
        a.ActVel = 0;
        a.MoveAbsCmd = false;
        if (homed || a.HomeCmd)
        {
            a.Homed = true;
            a.HomeCmd = false;
        }
    }

    /// <summary>软限位检测 → 触发限位报警</summary>
    private void CheckSoftLimits()
    {
        CheckAxisLimit(_db.Axes[(int)AxisId.X], S7DataBlock.AlarmBit.X_LimitPos, S7DataBlock.AlarmBit.X_LimitNeg);
        CheckAxisLimit(_db.Axes[(int)AxisId.Y], S7DataBlock.AlarmBit.Y_LimitPos, S7DataBlock.AlarmBit.Y_LimitNeg);
        CheckAxisLimit(_db.Axes[(int)AxisId.Z], S7DataBlock.AlarmBit.Z_LimitPos, S7DataBlock.AlarmBit.Z_LimitNeg);
    }

    private void CheckAxisLimit(AxisBlock a, int posBit, int negBit)
    {
        if (a.ActPos >= a.SoftLimitPos)
        {
            if (!a.LimitPos)
            {
                a.LimitPos = true;
                _db.AlarmWord[posBit] = true;
                Log.Warning("[软PLC] {Axis} 触发正软限位 ({Pos:F2}mm)", a.Axis, a.ActPos);
            }
        }
        else a.LimitPos = false;

        if (a.ActPos <= a.SoftLimitNeg)
        {
            if (!a.LimitNeg)
            {
                a.LimitNeg = true;
                _db.AlarmWord[negBit] = true;
                Log.Warning("[软PLC] {Axis} 触发负软限位 ({Pos:F2}mm)", a.Axis, a.ActPos);
            }
        }
        else a.LimitNeg = false;
    }

    public void Dispose()
    {
        _running = false;
        IsConnected = false;
        _scanThread?.Join(1000);
        Log.Information("[软PLC] 已停止");
    }
}
