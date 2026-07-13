using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace 点胶机.Logging;

/// <summary>
/// 全局 Module 属性 Enricher
/// 从日志消息开头的 "[模块名]" 前缀自动提取 Module 属性,
/// 供 MySqlLogSink / SerilogToastSink 统一使用(无需调用方手动 ForContext)。
///
/// 约定:本项目日志统一写成 "Log.Information("[调度器] 注册任务: {Name}", ...)",
/// 渲染后形如 "[调度器] 注册任务: 任务A" —— 提取 "调度器" 作为 Module。
///
/// 规则:
///   1. 仅匹配消息开头的第一段方括号 [xxx],中途出现的方括号不提取。
///   2. 跳过 "[==== ...]" 这种分隔线(如 "===== 系统启动 =====")。
///   3. 若消息已显式带 SourceContext 或 Module 属性,优先尊重既有值。
/// </summary>
public sealed class ModuleEnricher : ILogEventEnricher
{
    // 匹配消息开头的 [非空内容],首字符不能是 '='(排除 ==== 分隔线)
    private static readonly Regex _prefixRegex = new(
        @"^\[(?<m>[^=\]][^\]]{0,15})\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // 若已有 Module / SourceContext,不覆盖
        if (logEvent.Properties.ContainsKey("Module") ||
            logEvent.Properties.ContainsKey("SourceContext"))
            return;

        var msg = logEvent.RenderMessage();
        var match = _prefixRegex.Match(msg);
        if (!match.Success) return;

        var module = match.Groups["m"].Value.Trim();
        if (string.IsNullOrWhiteSpace(module)) return;

        // 占位符变量名(未渲染成功)忽略,避免把 {Task} 这种当模块名
        if (module.StartsWith('{') || module.Contains('{')) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("Module", module));
    }
}
