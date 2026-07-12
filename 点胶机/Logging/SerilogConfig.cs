using System.IO;
using Serilog;
using Serilog.Events;
using 点胶机.Core.Config;

namespace 点胶机.Logging;

/// <summary>
/// Serilog 配置 —— File Sink(按天分文件)
/// MySQL 落库由 Data 层的自定义 Sink 在第5阶段接入(用 Dapper 写 Logs 表)
/// </summary>
public static class SerilogConfig
{
    /// <summary>配置的最低日志级别(第5阶段重建 Logger 时复用)</summary>
    public static LogEventLevel MinimumLevel { get; private set; } = LogEventLevel.Debug;

    /// <summary>当前 File Sink 的日志文件路径模板(第5阶段重建 Logger 时复用)</summary>
    public static string FilePath { get; private set; } = "logs/log.txt";

    /// <summary>MySQL 落库 Sink 是否已挂载</summary>
    public static bool MySqlSinkAttached { get; private set; }

    public static void Configure(LoggingSection logging)
    {
        MinimumLevel = ParseLevel(logging.MinLevel);
        FilePath = logging.FilePath;

        // 确保 logs 目录存在
        var dir = Path.GetDirectoryName(logging.FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var cfg = new LoggerConfiguration()
            .MinimumLevel.Is(MinimumLevel)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.FromLogContext()
            // File Sink:按天分文件,保留 15 天
            .WriteTo.File(
                path: logging.FilePath.Replace(".txt", "-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        Log.Logger = cfg.CreateLogger();
        Log.Information("===== Serilog 已启动(File Sink)=====");
    }

    /// <summary>第5阶段:用 File + MySQL 双 Sink 重建全局 Logger</summary>
    public static void AttachMySqlSink(Serilog.Core.ILogEventSink mySqlSink)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(MinimumLevel)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: FilePath.Replace(".txt", "-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(mySqlSink)
            .CreateLogger();

        MySqlSinkAttached = true;
        Log.Information("MySQL Sink 已挂载,日志同步写入数据库");
    }

    private static LogEventLevel ParseLevel(string? s) => s?.ToLowerInvariant() switch
    {
        "verbose" or "trace" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "info" or "information" => LogEventLevel.Information,
        "warn" or "warning" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" => LogEventLevel.Fatal,
        _ => LogEventLevel.Debug
    };
}
