using System.Collections.Concurrent;
using MySqlConnector;
using Serilog.Core;
using Serilog.Events;

namespace 点胶机.Data;

/// <summary>
/// 自定义 Serilog MySQL Sink —— 用队列 + 后台批量写入,不阻塞调用线程
/// 写入 Logs 表(Timestamp/Level/Module/Message/Exception/MachineName)
/// </summary>
public sealed class MySqlLogSink : ILogEventSink, IDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentQueue<LogEvent> _queue = new();
    private readonly Thread _writerThread;
    private volatile bool _running = true;
    private readonly ManualResetEventSlim _flushSignal = new(false);

    public MySqlLogSink(string connectionString)
    {
        _connectionString = connectionString;
        _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "MySqlLogSink" };
        _writerThread.Start();
    }

    public void Emit(LogEvent logEvent)
    {
        // 入队即返回,后台线程批量写
        _queue.Enqueue(logEvent);
        _flushSignal.Set();
    }

    private void WriterLoop()
    {
        while (_running)
        {
            _flushSignal.Wait(1000);
            _flushSignal.Reset();

            if (_queue.IsEmpty) continue;

            // 取出一批
            var batch = new List<LogEvent>();
            while (_queue.TryDequeue(out var le))
            {
                batch.Add(le);
                if (batch.Count >= 50) break;
            }

            if (batch.Count == 0) continue;

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                foreach (var le in batch)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = """
                        INSERT INTO `Logs` (`Timestamp`,`Level`,`Module`,`Message`,`Exception`,`MachineName`)
                        VALUES (@ts,@lvl,@mod,@msg,@exc,@mach);
                        """;
                    cmd.Parameters.AddWithValue("@ts", le.Timestamp.LocalDateTime);
                    cmd.Parameters.AddWithValue("@lvl", le.Level.ToString());
                    cmd.Parameters.AddWithValue("@mod", ExtractModule(le));
                    cmd.Parameters.AddWithValue("@msg", le.RenderMessage());
                    cmd.Parameters.AddWithValue("@exc", le.Exception?.ToString() ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@mach", le.Properties.TryGetValue("MachineName", out var mn) ? mn.ToString().Trim('"') : (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // 数据库写入失败:丢弃本批(已落文件日志),避免日志线程阻塞业务
                // 可考虑重入队,但为避免无限堆积,这里直接丢弃
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _flushSignal.Set();
        _writerThread.Join(3000);
    }

    /// <summary>
    /// 从日志事件提取模块名(SourceContext 优先 → Module 属性 → 短名截取)。
    /// 由 ModuleEnricher 统一注入 Module 属性,此处只做兜底解析与短名截取。
    /// 返回 DBNull 表示无模块信息(供数据库写入 NULL)。
    /// </summary>
    internal static object ExtractModule(LogEvent le)
    {
        string? raw = null;
        if (le.Properties.TryGetValue("SourceContext", out var sc))
            raw = sc.ToString().Trim('"');
        else if (le.Properties.TryGetValue("Module", out var m))
            raw = m.ToString().Trim('"');

        if (string.IsNullOrWhiteSpace(raw))
            return DBNull.Value;

        // 命名空间取最后一段(如 点胶机.Tasks.Task_Dispensing → Task_Dispensing)
        if (raw.Contains('.'))
            raw = raw.Substring(raw.LastIndexOf('.') + 1);

        return string.IsNullOrWhiteSpace(raw) ? DBNull.Value : raw;
    }
}
