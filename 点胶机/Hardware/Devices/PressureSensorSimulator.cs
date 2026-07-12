using 点胶机.Core.Interfaces;

namespace 点胶机.Hardware.Devices;

/// <summary>
/// 压力传感器仿真器 —— 返回带轻微波动的模拟气压值,用于报警监听演示
/// </summary>
public sealed class PressureSensorSimulator : IPressureSensor
{
    private readonly double _basePressure;
    private readonly Random _rnd = new();
    private double _faultChance;   // 故障概率(0~1),可由设置页调高以演示气压报警

    public PressureSensorSimulator(double basePressureKpa = 500)
    {
        _basePressure = basePressureKpa;
    }

    /// <summary>设置故障概率(0=正常, 1=始终低压);用于演示气压低报警</summary>
    public void SetFaultChance(double chance) => _faultChance = Math.Clamp(chance, 0, 1);

    public double GetValue()
    {
        // 正常:基准 ± 5kpa 波动;故障时按概率跌到低压
        if (_rnd.NextDouble() < _faultChance)
            return _basePressure * 0.3;   // 低压
        return _basePressure + (_rnd.NextDouble() - 0.5) * 10;
    }
}
