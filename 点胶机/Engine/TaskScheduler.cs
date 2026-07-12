using Serilog;
using 点胶机.Core.Enums;

namespace 点胶机.Engine;

/// <summary>
/// 任务调度器 —— N 个后台线程协作轮询所有 Task,每个任务同一时刻只被一个线程 Tick(加锁保护)
/// 对齐 AutoStudio.Service.IAutoTaskService(线程池轮询模型)
/// 关键:为避免多线程并发修改同一状态机,对每个 Task 的 Tick 加 per-task 锁
/// </summary>
public sealed class TaskScheduler : IDisposable
{
    private readonly List<TaskBase> _tasks = new();
    private readonly List<Thread> _threads = new();
    private readonly List<object> _taskLocks = new();   // per-task 锁
    private volatile bool _running;
    private int _threadCount = 4;
    private int _tickIntervalMs = 1;

    /// <summary>设置工作线程数</summary>
    public TaskScheduler SetThreadNumber(int n) { _threadCount = Math.Max(1, n); return this; }

    /// <summary>设置每轮循环间隔(ms)</summary>
    public TaskScheduler SetThreadSleepTime(int ms) { _tickIntervalMs = Math.Max(1, ms); return this; }

    /// <summary>添加任务到调度器</summary>
    public TaskScheduler AddTask(TaskBase task, bool monitored = true)
    {
        task.IsMonitored = monitored;
        _tasks.Add(task);
        _taskLocks.Add(new object());
        Log.Information("[调度器] 注册任务: {Name} (监控={Monitored})", task.Name, monitored);
        return this;
    }

    /// <summary>启动所有工作线程</summary>
    public TaskScheduler Start()
    {
        if (_running) return this;
        _running = true;

        for (int i = 0; i < _threadCount; i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"TaskWorker-{i}"
            };
            _threads.Add(thread);
            thread.Start();
        }
        Log.Information("[调度器] 已启动 {N} 个工作线程,共 {TaskCount} 个任务", _threadCount, _tasks.Count);
        return this;
    }

    /// <summary>停止所有工作线程</summary>
    public void Stop()
    {
        _running = false;
        foreach (var t in _threads)
        {
            if (t.IsAlive) t.Join(2000);
        }
        _threads.Clear();
        Log.Information("[调度器] 已停止");
    }

    private void WorkerLoop(object? state)
    {
        while (_running)
        {
            try
            {
                var mode = TaskStatic.Instance.WorkMode;

                for (int i = 0; i < _tasks.Count; i++)
                {
                    if (!_running) break;
                    var task = _tasks[i];
                    if (!task.IsEnabled) continue;

                    var lk = _taskLocks[i];

                    // per-task 锁:保证同一任务同一时刻只被一个线程 Tick(状态机不重入)
                    lock (lk)
                    {
                        task.Tick(mode);
                    }
                }

                if (_tickIntervalMs > 0)
                    Thread.Sleep(_tickIntervalMs);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[调度器] 工作线程异常");
                Thread.Sleep(10);
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
