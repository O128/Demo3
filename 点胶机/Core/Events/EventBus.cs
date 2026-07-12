using System.Windows.Threading;
using 点胶机.Core.Interfaces;

namespace 点胶机.Core.Events;

/// <summary>
/// 事件总线实现 —— 进程内订阅/发布,自动切换到 UI 线程
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(TEvent)] = list;
            }
            list.Add(handler);
        }
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : notnull
    {
        List<Delegate>? handlers;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list)) return;
            handlers = list.ToList(); // 快照,避免回调里增删导致枚举异常
        }

        foreach (var h in handlers)
        {
            if (h is Action<TEvent> action)
            {
                // UI 事件订阅者大多在 UI 线程,但发布者常在任务线程,这里异步切到 UI 线程
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try { action(@event); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"事件处理异常: {ex}"); }
                }), DispatcherPriority.DataBind);
            }
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list.Remove(handler);
            }
        }
    }
}
