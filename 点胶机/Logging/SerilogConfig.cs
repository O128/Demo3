using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using 点胶机.Core.Config;

namespace 点胶机.Logging;

/// <summary>
/// Serilog 配置 —— File Sink(按天分文件)+ 可累积挂载多个额外 Sink(MySQL/Toast 等)
/// </summary>
public static class SerilogConfig
{
    public static LogEventLevel MinimumLevel { get; private set; } = LogEventLevel.Debug;
    public static string FilePath { get; private set; } = "logs/log.txt";

    /// <summary>已挂载的额外 Sink 列表(累积,重建 Logger 时全部保留)</summary>
    private static readonly List<ILogEventSink> _extraSinks = new();

    public static bool MySqlSinkAttached { get; private set; }

    public static void Configure(LoggingSection logging)
    {
        MinimumLevel = ParseLevel(logging.MinLevel);
        FilePath = logging.FilePath;

        var dir = Path.GetDirectoryName(logging.FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        RebuildLogger();
        Log.Information("===== Serilog 已启动(File Sink)=====");
    }

    /// <summary>挂载一个额外 Sink(MySQL/Toast 等),累积保留所有已挂载的 Sink</summary>
    public static void AttachSink(ILogEventSink sink, bool isMySql = false)
    {
        if (sink is null) return;
        _extraSinks.Add(sink);
        if (isMySql) MySqlSinkAttached = true;
        RebuildLogger();
        Log.Information("Sink 已挂载(当前共 {N} 个额外 Sink)", _extraSinks.Count);
    }

    /// <summary>兼容旧接口:挂载 MySQL Sink</summary>
    public static void AttachMySqlSink(ILogEventSink sink) => AttachSink(sink, isMySql: true);

    private static void RebuildLogger()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var cfg = new LoggerConfiguration()
            .MinimumLevel.Is(MinimumLevel)
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.With<ModuleEnricher>()           // 自动从 "[模块名]" 前缀提取 Module 属性
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: FilePath.Replace(".txt", "-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 15,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

        // 把所有累积的额外 Sink 都加上(File + MySQL + Toast ...)
        foreach (var s in _extraSinks)
            cfg = cfg.WriteTo.Sink(s);

        Log.Logger = cfg.CreateLogger();
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
