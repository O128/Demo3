namespace 点胶机.Engine;

/// <summary>
/// 任务间握手信号 —— 用于"任务A 等任务B 物料/结果到位"的同步
/// 对齐 AutoStudio 的 HandEvent
/// </summary>
public class HandEvent
{
    private volatile bool _signaled;

    /// <summary>触发信号</summary>
    public void Set() => _signaled = true;

    /// <summary>清除信号</summary>
    public void Reset() => _signaled = false;

    /// <summary>信号是否已触发</summary>
    public bool IsSignaled => _signaled;

    /// <summary>等待信号触发;每 sleepMs 自旋一次,超时返回 false。状态机里用 Wait(timeoutMs) 判断。</summary>
    public bool Wait(int timeoutMs, int sleepMs = 1)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!_signaled)
        {
            if (sw.ElapsedMilliseconds >= timeoutMs) return false;
            Thread.Sleep(sleepMs);
        }
        return true;
    }
}
