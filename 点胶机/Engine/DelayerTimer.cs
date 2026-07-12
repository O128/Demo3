using System.Diagnostics;

namespace 点胶机.Engine;

/// <summary>
/// 非阻塞延时定时器 —— 状态机里等待的核心工具
/// 工作原理:Start(ms) 首次调用记录起始时间并返回 false;
/// 之后每周期再调用,若已到时则返回 true 并自动复位(可再次 Start)。
/// 契合任务调度器的轮询模型,不阻塞线程。
/// </summary>
public class DelayerTimer
{
    private long _startTicks;      // Stopwatch.GetTimestamp() 的起始读数
    private double _targetMs;      // 目标延时(毫秒)
    private bool _running;          // 是否正在计时

    /// <summary>启动一次延时。首次调用记录起始并返回 false;到时返回 true 并自动复位。</summary>
    public bool Start(double ms)
    {
        if (!_running)
        {
            _startTicks = Stopwatch.GetTimestamp();
            _targetMs = ms;
            _running = true;
            return false;
        }

        var elapsedMs = (Stopwatch.GetTimestamp() - _startTicks) * 1000.0 / Stopwatch.Frequency;
        if (elapsedMs >= _targetMs)
        {
            _running = false;   // 自动复位
            return true;
        }
        return false;
    }

    /// <summary>手动复位</summary>
    public void Reset() => _running = false;

    /// <summary>是否正在计时</summary>
    public bool IsRunning => _running;

    /// <summary>已计时毫秒(未启动返回 0)</summary>
    public double ElapsedMs
    {
        get
        {
            if (!_running) return 0;
            return (Stopwatch.GetTimestamp() - _startTicks) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
