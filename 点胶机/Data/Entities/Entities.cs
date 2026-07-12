namespace 点胶机.Data.Entities;

/// <summary>
/// 日志实体(对应 Logs 表,由 Serilog MySQL Sink 写入)
/// </summary>
public class LogEntity
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string? Module { get; set; }
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string? MachineName { get; set; }
}

/// <summary>
/// 报警历史实体(对应 Alarms 表)
/// </summary>
public class AlarmEntity
{
    public long Id { get; set; }
    public int AlarmId { get; set; }
    public string AlarmName { get; set; } = "";
    public string Level { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DurationSec { get; set; }
    public DateTime? AckTime { get; set; }
    public string? AckUser { get; set; }
}

/// <summary>
/// 生产记录实体(对应 Production 表)
/// </summary>
public class ProductionEntity
{
    public long Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? CycleTime { get; set; }
    public string Result { get; set; } = "OK";
    public string? RecipeName { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double GlueAmount { get; set; }
}
