using Serilog;
using 点胶机.Core.Config;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Hardware.Devices;
using 点胶机.Hardware.Motion;
using 点胶机.Hardware.Plc;

namespace 点胶机.Hardware;

/// <summary>
/// 硬件管理器 —— 统一初始化/启停所有硬件(软 PLC、运动卡、外设)
/// 启动顺序:软PLC扫描线程 → 下发轴参数 → 伺服上电(延迟到初始化任务)
/// </summary>
public sealed class HardwareManager : IDisposable
{
    private readonly AppConfig _config;
    public PlcSimulator Plc { get; }
    public MotionCardPlc Motion { get; }
    public GlueValveSimulator GlueValve { get; }
    public PressureSensorSimulator Pressure { get; }

    public HardwareManager(AppConfig config)
    {
        _config = config;
        Plc = new PlcSimulator(_config.Hardware.Plc.ScanCycleMs);
        Motion = new MotionCardPlc(Plc);
        GlueValve = new GlueValveSimulator();
        Pressure = new PressureSensorSimulator();
    }

    /// <summary>初始化硬件:启动软 PLC + 下发轴参数</summary>
    public void Init()
    {
        Log.Information("===== 硬件初始化开始 =====");

        // 1. 启动软 PLC 扫描线程
        Plc.Start();

        // 2. 下发轴参数(从配置加载)
        ApplyAxisParameters();

        Log.Information("===== 硬件初始化完成 =====");
    }

    private void ApplyAxisParameters()
    {
        foreach (AxisId axis in Enum.GetValues(typeof(AxisId)))
        {
            var key = axis.ToString();
            if (!_config.Axes.TryGetValue(key, out var sec))
            {
                Log.Warning("[硬件] 轴 {Axis} 未配置参数,使用默认值", axis);
                continue;
            }

            var param = new AxisParameter
            {
                Axis = axis,
                PulseUnit = sec.PulseUnit,
                SoftLimitPositive = sec.SoftLimitPositive,
                SoftLimitNegative = sec.SoftLimitNegative,
                RunSpeed = sec.RunSpeed,
                HomeSpeed = sec.HomeSpeed
            };
            Motion.ApplyAxisParameter(axis, param);

            // 同步到软 PLC 的数据块
            var ab = Plc.DataBlock.Axes[(int)axis];
            ab.PulseUnit = param.PulseUnit;
            ab.SoftLimitPos = param.SoftLimitPositive;
            ab.SoftLimitNeg = param.SoftLimitNegative;
            ab.RunSpeed = param.RunSpeed;
            ab.HomeSpeed = param.HomeSpeed;

            Log.Information("[硬件] {Axis} 参数: 限位[{Min:F1},{Max:F1}]mm, 运行速度 {Vel:F0}mm/s, 回零速度 {Home:F0}mm/s",
                axis, param.SoftLimitNegative, param.SoftLimitPositive, param.RunSpeed, param.HomeSpeed);
        }
    }

    /// <summary>伺服上电全部轴(由初始化任务调用)</summary>
    public void ServoOnAll()
    {
        foreach (AxisId axis in Enum.GetValues(typeof(AxisId)))
        {
            Motion.ServoOn(axis);
        }
    }

    /// <summary>伺服下电全部轴</summary>
    public void ServoOffAll()
    {
        foreach (AxisId axis in Enum.GetValues(typeof(AxisId)))
        {
            Motion.ServoOff(axis);
        }
    }

    public void Dispose()
    {
        ServoOffAll();
        Plc.Dispose();
        Log.Information("[硬件] 已释放");
    }
}
