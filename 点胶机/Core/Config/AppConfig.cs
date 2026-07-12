namespace 点胶机.Core.Config;

/// <summary>
/// 应用配置根模型(对应 appsettings.json 结构)
/// </summary>
public class AppConfig
{
    public MachineSection Machine { get; set; } = new();
    public HardwareSection Hardware { get; set; } = new();
    public Dictionary<string, AxisSection> Axes { get; set; } = new();
    public DatabaseSection Database { get; set; } = new();
    public LoggingSection Logging { get; set; } = new();
    public SchedulerSection Scheduler { get; set; } = new();

    /// <summary>联调自启动测试开关(阶段7验证用;生产环境设为 false,通过 UI 按钮启动)</summary>
    public bool AutoStartForTest { get; set; } = false;
}

public class MachineSection
{
    public string Name { get; set; } = "Dispenser-01";
    public string Version { get; set; } = "V1.0.0";
}

public class HardwareSection
{
    public bool NoHardwareMode { get; set; } = true;
    public PlcSection Plc { get; set; } = new();
    public VisionSection Vision { get; set; } = new();
}

public class PlcSection
{
    public string Mode { get; set; } = "Sim";
    public int ScanCycleMs { get; set; } = 1;
}

public class VisionSection
{
    /// <summary>视觉纯仿真延时(ms)</summary>
    public int SimDelayMs { get; set; } = 500;
}

public class AxisSection
{
    public double PulseUnit { get; set; } = 1000;
    public double SoftLimitPositive { get; set; } = 300;
    public double SoftLimitNegative { get; set; } = -10;
    public double RunSpeed { get; set; } = 100;
    public double HomeSpeed { get; set; } = 20;
}

public class DatabaseSection
{
    public string ConnectionString { get; set; } =
        "Server=localhost;Port=3306;Database=dispenser;Uid=root;Pwd=123456;";
    public bool AutoCreateTable { get; set; } = true;
}

public class LoggingSection
{
    public string MinLevel { get; set; } = "Debug";
    public string FilePath { get; set; } = "logs/log.txt";
}

public class SchedulerSection
{
    public int ThreadCount { get; set; } = 4;
    public int TickIntervalMs { get; set; } = 1;
}
