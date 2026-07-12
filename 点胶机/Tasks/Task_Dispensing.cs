using Serilog;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Engine;
using 点胶机.Hardware;
using 点胶机.Recipe;

namespace 点胶机.Tasks;

/// <summary>
/// 点胶主流程任务 —— 单工位桌面点胶机状态机
/// 工艺:Z升上位 → XY到拍照位 → 视觉定位(纯延时) → XY到点胶起点 → Z下降 → 按轨迹点胶 → Z上升 → 回Home → 完成
///
/// 步序模式:每个运动环节拆成两个 case
///   发起步:下发 MoveAbsolute + SetStep(下一步)  + 启动 TimeoutTimer
///   等待步:检查 IsInPosition;若 TimeoutTimer 到时仍未到位 → Fail
/// 这样每个 case 逻辑独立,无状态混淆
/// </summary>
public sealed class Task_Dispensing : TaskBase
{
    private readonly HardwareManager _hw;
    private readonly RecipeManager _recipe;
    private readonly IGlueValve _glue;
    private readonly Data.ProductionRepository? _prodRepo;
    private readonly int _visionDelayMs;

    private int _pointIndex;
    private DateTime _cycleStart;
    // 视觉偏移(纯仿真,恒为0)
    private double _offsetX, _offsetY;
    // 等待步的超时计时(发起步记录开始时间)
    private long _waitStartTicks;
    private int _waitTimeoutMs;
    private string _waitFailReason = "";

    public Task_Dispensing(HardwareManager hw, RecipeManager recipe, IGlueValve glue, int visionDelayMs,
                           Data.ProductionRepository? prodRepo = null)
        : base("点胶主流程")
    {
        _hw = hw;
        _recipe = recipe;
        _glue = glue;
        _visionDelayMs = visionDelayMs;
        _prodRepo = prodRepo;
    }

    protected override void AutoRun(WorkMode mode)
    {
        var ts = TaskStatic.Instance;
        var r = _recipe.Current;
        bool emptyRun = mode == WorkMode.EmptyRun;

        switch (WorkStep)
        {
            // ===== 0:空闲,等待启动 =====
            case 0:
                if (ts.RunStatus == RunStatus.Running)
                {
                    _cycleStart = DateTime.Now;
                    _pointIndex = 0;
                    _glue.ResetAccumulator();
                    SetStep(10, "Z 轴升上位");
                }
                break;

            // ----- 10:Z 升到安全高度 -----
            case 10:
                if (emptyRun || _hw.Motion.MoveAbsolute(AxisId.Z, r.ZSafeHeight, r.DispenseSpeed))
                {
                    StartWait(10000, "Z 轴升上位超时");
                    SetStep(11, "Z 轴上升中");
                }
                break;
            case 11:
                if (ArrivedOrTimeout(emptyRun, AxisId.Z)) SetStep(20, "XY 移到拍照位");
                break;

            // ----- 20:XY 到拍照位 -----
            case 20:
                _hw.Motion.MoveAbsolute(AxisId.X, r.PhotoPosX, r.DispenseSpeed);
                _hw.Motion.MoveAbsolute(AxisId.Y, r.PhotoPosY, r.DispenseSpeed);
                StartWait(15000, "XY 到拍照位超时");
                SetStep(21, "XY 移到拍照位");
                break;
            case 21:
                if (ArrivedOrTimeout(emptyRun, AxisId.X, AxisId.Y)) SetStep(30, "视觉定位");
                break;

            // ===== 30:视觉定位(纯仿真延时,无图像/算法/偏移)=====
            case 30:
                if (Timer.Start(_visionDelayMs))
                {
                    _offsetX = 0;
                    _offsetY = 0;
                    Log.Information("[视觉] 定位完成(纯仿真延时 {Ms}ms),偏移=0", _visionDelayMs);
                    SetStep(40, "XY 移到点胶起点");
                }
                break;

            // ----- 40:XY 到第一个点胶点 -----
            case 40:
                if (r.Points.Count == 0) { Fail("配方轨迹点为空"); break; }
                var first = r.Points[0];
                _hw.Motion.MoveAbsolute(AxisId.X, first.X + _offsetX, r.DispenseSpeed);
                _hw.Motion.MoveAbsolute(AxisId.Y, first.Y + _offsetY, r.DispenseSpeed);
                StartWait(15000, "XY 到点胶起点超时");
                SetStep(41, "XY 移到点胶起点");
                break;
            case 41:
                if (ArrivedOrTimeout(emptyRun, AxisId.X, AxisId.Y)) SetStep(50, "Z 轴降到点胶高度");
                break;

            // ----- 50:Z 下降到点胶高度 -----
            case 50:
                if (emptyRun || _hw.Motion.MoveAbsolute(AxisId.Z, r.ZDispenseHeight, r.DispenseSpeed))
                {
                    StartWait(10000, "Z 轴下降超时");
                    SetStep(51, "Z 轴下降中");
                }
                break;
            case 51:
                if (ArrivedOrTimeout(emptyRun, AxisId.Z)) SetStep(60, "执行点胶轨迹");
                break;

            // ===== 60/61:遍历轨迹点(逐点移动 + 胶阀开/关)=====
            case 60:
                if (_pointIndex >= r.Points.Count)
                {
                    GlueClose();
                    SetStep(70, "Z 轴上升");
                    break;
                }
                var pt = r.Points[_pointIndex];
                if (pt.GlueOn) GlueOpen(); else GlueClose();
                _hw.Motion.MoveAbsolute(AxisId.X, pt.X + _offsetX, r.DispenseSpeed);
                _hw.Motion.MoveAbsolute(AxisId.Y, pt.Y + _offsetY, r.DispenseSpeed);
                StartWait(15000, $"轨迹点 {_pointIndex + 1} 到位超时");
                SetStep(61, $"点胶轨迹 {_pointIndex + 1}/{r.Points.Count}");
                break;
            case 61:
                if (ArrivedOrTimeout(emptyRun, AxisId.X, AxisId.Y))
                {
                    _pointIndex++;
                    if (_pointIndex >= r.Points.Count)
                    {
                        GlueClose();
                        SetStep(70, "Z 轴上升");
                    }
                    else
                    {
                        SetStep(60, $"点胶轨迹 {_pointIndex + 1}/{r.Points.Count}");
                    }
                }
                break;

            // ----- 70:Z 上升 -----
            case 70:
                if (emptyRun || _hw.Motion.MoveAbsolute(AxisId.Z, r.ZSafeHeight, r.DispenseSpeed))
                {
                    StartWait(10000, "Z 轴上升超时");
                    SetStep(71, "Z 轴上升中");
                }
                break;
            case 71:
                if (ArrivedOrTimeout(emptyRun, AxisId.Z)) SetStep(80, "产量计数");
                break;

            // ===== 80:产量计数 =====
            case 80:
                ts.TodayYield++;
                var cycleSec = (DateTime.Now - _cycleStart).TotalSeconds;
                Log.Information("[生产] 第 {N} 片完成,周期 {Sec:F2}s,胶量 {Glue:F1}mg",
                    ts.TodayYield, cycleSec, _glue.AccumulatedAmount);
                // 写生产记录到 MySQL
                try
                {
                    _prodRepo?.Insert(_cycleStart, DateTime.Now, cycleSec, "OK",
                        r.Name, _offsetX, _offsetY, _glue.AccumulatedAmount);
                }
                catch (Exception ex) { Log.Error(ex, "写生产记录失败"); }
                SetStep(90, "回 Home");
                break;

            // ----- 90:各轴回 Home -----
            case 90:
                _hw.Motion.MoveAbsolute(AxisId.X, r.HomeX, r.DispenseSpeed);
                _hw.Motion.MoveAbsolute(AxisId.Y, r.HomeY, r.DispenseSpeed);
                _hw.Motion.MoveAbsolute(AxisId.Z, r.HomeZ, r.DispenseSpeed);
                StartWait(15000, "回 Home 超时");
                SetStep(91, "回 Home 中");
                break;
            case 91:
                if (ArrivedOrTimeout(emptyRun, AxisId.X, AxisId.Y, AxisId.Z)) SetStep(100, "本片完成");
                break;

            // ===== 100:本片完成,循环下一片 =====
            case 100:
                SetStep(0, "等待下一片");
                break;

            // ===== 1000:异常,等复位 =====
            case 1000:
                StepDesc = "异常,等待复位";
                break;
        }
    }

    /// <summary>启动一次等待计时(在发起步调用,记录开始时间和超时阈值)</summary>
    private void StartWait(int timeoutMs, string failReason)
    {
        _waitStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        _waitTimeoutMs = timeoutMs;
        _waitFailReason = failReason;
    }

    /// <summary>
    /// 检查指定轴是否全部到位;若空跑模式直接返回 true;若超时则 Fail 并返回 false
    /// 调用前需已通过 StartWait 启动计时
    /// </summary>
    private bool ArrivedOrTimeout(bool emptyRun, params AxisId[] axes)
    {
        if (emptyRun) return true;

        // 全部到位?
        bool allArrived = true;
        foreach (var axis in axes)
        {
            if (!_hw.Motion.IsInPosition(axis) || _hw.Motion.IsMoving(axis))
            {
                allArrived = false;
                break;
            }
        }
        if (allArrived) return true;

        // 检查超时
        var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - _waitStartTicks) * 1000.0
                        / System.Diagnostics.Stopwatch.Frequency;
        if (elapsedMs >= _waitTimeoutMs)
        {
            Fail(_waitFailReason);
        }
        return false;
    }

    /// <summary>失败:进入异常步序,触发报警</summary>
    private void Fail(string reason)
    {
        Log.Error("[点胶] 失败: {Reason}", reason);
        WorkStep = 1000;
        StepDesc = reason;
        TaskStatic.Instance.IsAlarm = true;
    }

    /// <summary>开胶阀并同步写 PLC 输出(IO 联动)</summary>
    private void GlueOpen()
    {
        _glue.Open();
        _hw.Plc.WriteOutput(IoIndex.Out_GlueValve, true);
    }

    /// <summary>关胶阀并同步写 PLC 输出(IO 联动)</summary>
    private void GlueClose()
    {
        _glue.Close();
        _hw.Plc.WriteOutput(IoIndex.Out_GlueValve, false);
    }

    protected override void OnStop()
    {
        GlueClose();
        foreach (AxisId axis in Enum.GetValues(typeof(AxisId)))
            _hw.Motion.Stop(axis);
        if (WorkStep != 1000)
        {
            WorkStep = 0;
            StepDesc = "停止";
            Timer.Reset();
        }
    }
}
