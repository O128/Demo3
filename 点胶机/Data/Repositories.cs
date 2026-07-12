using Dapper;
using MySqlConnector;
using 点胶机.Data.Entities;

namespace 点胶机.Data;

/// <summary>日志仓储 —— 查询 Logs 表(写入由 Serilog Sink 负责)</summary>
public sealed class LogRepository
{
    private readonly string _connStr;
    public LogRepository(string connStr) => _connStr = connStr;

    /// <summary>分页查询日志(按时间倒序)</summary>
    public List<LogEntity> Query(DateTime? from = null, DateTime? to = null, string? level = null, string? module = null, int limit = 500)
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        var sql = "SELECT * FROM `Logs` WHERE 1=1";
        var p = new DynamicParameters();
        if (from.HasValue) { sql += " AND Timestamp >= @from"; p.Add("@from", from.Value); }
        if (to.HasValue) { sql += " AND Timestamp <= @to"; p.Add("@to", to.Value); }
        if (!string.IsNullOrEmpty(level)) { sql += " AND Level = @lvl"; p.Add("@lvl", level); }
        if (!string.IsNullOrEmpty(module)) { sql += " AND Module LIKE @mod"; p.Add("@mod", $"%{module}%"); }
        sql += " ORDER BY Timestamp DESC LIMIT @lim";
        p.Add("@lim", limit);
        return conn.Query<LogEntity>(sql, p).ToList();
    }
}

/// <summary>报警历史仓储</summary>
public sealed class AlarmRepository
{
    private readonly string _connStr;
    public AlarmRepository(string connStr) => _connStr = connStr;

    /// <summary>记录一条报警触发</summary>
    public void InsertStart(int alarmId, string name, string level, DateTime startTime, string? message = null)
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        conn.Execute(
            "INSERT INTO `Alarms` (`AlarmId`,`AlarmName`,`Level`,`StartTime`) VALUES (@id,@name,@lvl,@st);",
            new { id = alarmId, name, lvl = level, st = startTime });
    }

    /// <summary>记录报警结束(确认)</summary>
    public void UpdateEnd(int alarmId, DateTime startTime, DateTime endTime, string ackUser = "operator")
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        var dur = (endTime - startTime).TotalSeconds;
        conn.Execute(
            @"UPDATE `Alarms` SET `EndTime`=@et, `DurationSec`=@dur, `AckTime`=@at, `AckUser`=@u
              WHERE `AlarmId`=@id AND `StartTime`=@st AND `EndTime` IS NULL
              ORDER BY `Id` DESC LIMIT 1;",
            new { id = alarmId, st = startTime, et = endTime, dur, at = endTime, u = ackUser });
    }

    /// <summary>分页查询报警历史</summary>
    public List<AlarmEntity> Query(DateTime? from = null, DateTime? to = null, string? level = null, int limit = 500)
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        var sql = "SELECT * FROM `Alarms` WHERE 1=1";
        var p = new DynamicParameters();
        if (from.HasValue) { sql += " AND StartTime >= @from"; p.Add("@from", from.Value); }
        if (to.HasValue) { sql += " AND StartTime <= @to"; p.Add("@to", to.Value); }
        if (!string.IsNullOrEmpty(level)) { sql += " AND Level = @lvl"; p.Add("@lvl", level); }
        sql += " ORDER BY StartTime DESC LIMIT @lim";
        p.Add("@lim", limit);
        return conn.Query<AlarmEntity>(sql, p).ToList();
    }
}

/// <summary>生产记录仓储</summary>
public sealed class ProductionRepository
{
    private readonly string _connStr;
    public ProductionRepository(string connStr) => _connStr = connStr;

    /// <summary>记录一片生产完成</summary>
    public void Insert(DateTime startTime, DateTime endTime, double cycleSec, string result, string? recipe, double offX, double offY, double glue)
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        conn.Execute(
            @"INSERT INTO `Production` (`StartTime`,`EndTime`,`CycleTime`,`Result`,`RecipeName`,`OffsetX`,`OffsetY`,`GlueAmount`)
              VALUES (@st,@et,@ct,@r,@rec,@ox,@oy,@g);",
            new { st = startTime, et = endTime, ct = cycleSec, r = result, rec = recipe, ox = offX, oy = offY, g = glue });
    }

    /// <summary>查询今日生产记录</summary>
    public List<ProductionEntity> QueryToday()
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        var today = DateTime.Today;
        return conn.Query<ProductionEntity>(
            "SELECT * FROM `Production` WHERE StartTime >= @t ORDER BY StartTime DESC LIMIT 500;",
            new { t = today }).ToList();
    }

    /// <summary>统计今日产量/良率</summary>
    public (int total, int ok) CountToday()
    {
        using var conn = new MySqlConnection(_connStr);
        conn.Open();
        var today = DateTime.Today;
        var rows = conn.Query<dynamic>(
            "SELECT Result AS r FROM `Production` WHERE StartTime >= @t;", new { t = today }).ToList();
        int total = rows.Count;
        int ok = rows.Count(x => x.r == "OK");
        return (total, ok);
    }
}
