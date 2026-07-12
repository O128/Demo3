using Serilog.Core;
using Serilog.Events;
using 点胶机.Services.Toast;

namespace 点胶机.Logging;

/// <summary>
/// Serilog → Toast 桥接 Sink
/// 把 Info 及以上级别(Info/Warning/Error/Fatal)的日志推到 ToastService 显示
/// Debug/Verbose 不显示(避免刷屏)
/// </summary>
public sealed class SerilogToastSink : ILogEventSink
{
    private readonly ToastService _toast;

    public SerilogToastSink(ToastService toast)
    {
        _toast = toast;
    }

    public void Emit(LogEvent logEvent)
    {
        // 只显示 Info 及以上(Debug/Verbose 不进 Toast)
        if (logEvent.Level < LogEventLevel.Information) return;

        var msg = logEvent.RenderMessage();

        // 过滤:急停相关日志不进 Toast(急停是操作员主动行为,不刷屏通知)
        if (msg.Contains("急停")) return;
        // 过滤:[报警触发]/[报警确认] 日志不进 Toast —— 这些由 AlarmEvent→DialogService 单独推送,避免重复
        if (msg.Contains("[报警触发]") || msg.Contains("[报警确认]")) return;

        // 解析模块名(SourceContext 或 Module 属性)
        string module = "";
        if (logEvent.Properties.TryGetValue("SourceContext", out var sc))
            module = sc.ToString().Trim('"');
        else if (logEvent.Properties.TryGetValue("Module", out var m))
            module = m.ToString().Trim('"');
        // 取短名(命名空间最后一段)
        if (module.Contains('.'))
            module = module.Substring(module.LastIndexOf('.') + 1);
        if (string.IsNullOrEmpty(module)) module = "系统";

        if (logEvent.Exception != null)
            msg += $"\n{logEvent.Exception.GetType().Name}: {logEvent.Exception.Message}";

        // 级别映射
        switch (logEvent.Level)
        {
            case LogEventLevel.Error:
            case LogEventLevel.Fatal:
                _toast.Error(module, msg);
                break;
            case LogEventLevel.Warning:
                _toast.Warning(module, msg);
                break;
            default:   // Information
                _toast.Info(module, msg);
                break;
        }
    }
}
