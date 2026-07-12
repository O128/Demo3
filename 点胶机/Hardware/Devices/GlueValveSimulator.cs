using System.Diagnostics;
using Serilog;
using 点胶机.Core.Interfaces;

namespace 点胶机.Hardware.Devices;

/// <summary>
/// 点胶阀仿真器 —— Open/Close 切换,开阀期间按流量累计胶量
/// </summary>
public sealed class GlueValveSimulator : IGlueValve
{
    private readonly double _flowMgPerMs;   // 流量(mg/ms)
    private readonly Stopwatch _openSw = new();
    private bool _isOpen;
    private double _accumulated;

    public GlueValveSimulator(double flowMgPerMs = 0.5)
    {
        _flowMgPerMs = flowMgPerMs;
    }

    public bool IsOpen => _isOpen;

    public double AccumulatedAmount
    {
        get
        {
            // 实时累加当前开阀时间
            if (_isOpen) _accumulated += _openSw.Elapsed.TotalMilliseconds * 0; // 由扫描周期更新
            return _accumulated;
        }
    }

    public void Open()
    {
        if (!_isOpen)
        {
            _isOpen = true;
            _openSw.Restart();
            Log.Information("[点胶阀] 打开,流量 {Flow} mg/ms", _flowMgPerMs);
        }
    }

    public void Close()
    {
        if (_isOpen)
        {
            _isOpen = false;
            _openSw.Stop();
            Log.Information("[点胶阀] 关闭");
        }
    }

    /// <summary>由扫描任务周期调用,累计胶量</summary>
    public void Tick(double dtMs)
    {
        if (_isOpen)
        {
            _accumulated += _flowMgPerMs * dtMs;
        }
    }

    public void ResetAccumulator()
    {
        _accumulated = 0;
    }
}
