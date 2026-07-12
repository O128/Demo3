using System.Windows;
using System.Windows.Threading;
using 点胶机.Core.Interfaces;

namespace 点胶机.Core.Events;

/// <summary>
/// 事件总线实现 —— 进程内订阅/发布,自动切换到 UI 线程
/// </summary>
public sealed class EventBus : IEventBus
{
    // 必须用 Application.Current.Dispatcher —— 始终是真正的 WPF UI 线程 Dispatcher。
    // 不能用 Dispatcher.CurrentDispatcher:它会在构造时为"当前线程"取/建 Dispatcher,
    // 若 EventBus 在非 UI 线程首次构造,会拿到一个无人驱动的 Dispatcher,
    // 导致 BeginInvoke 投递的事件永不执行(弹窗/状态更新全部失效)。
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;
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
