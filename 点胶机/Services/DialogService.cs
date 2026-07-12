using System.Windows;
using System.Windows.Threading;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.ViewModels;
using 点胶机.Views.Dialogs;

namespace 点胶机.Services;

/// <summary>
/// 对话框服务实现 —— 创建 NotifyWindow 弹窗显示提示/警报
/// 线程安全:所有 UI 操作经 Dispatcher 切到 UI 线程
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    /// <summary>当前已弹出的报警窗口集合(按 alarmId 去重)</summary>
    private static readonly HashSet<int> _shownAlarms = new();
    private static readonly object _shownLock = new();

    public void ShowAlarm(int alarmId, string name, AlarmLevel level, string message)
    {
        // 去重:同一报警不重复弹
        lock (_shownLock)
        {
            if (_shownAlarms.Contains(alarmId)) return;
            _shownAlarms.Add(alarmId);
        }

        _dispatcher.BeginInvoke(new Action(() =>
        {
            var vm = new NotifyDialogViewModel();
            vm.Configure(NotifyDialogViewModel.DialogKind.Alarm, $"[{level}] #{alarmId} {name}\n{message}");
            var win = new NotifyWindow { DataContext = vm, Title = $"报警 #{alarmId}" };
            vm.OwnerWindow = win;
            // 报警用模态(阻塞直到确认)
            win.ShowDialog();
            lock (_shownLock) { _shownAlarms.Remove(alarmId); }
        }), DispatcherPriority.Normal);
    }

    public void ShowTip(string message)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            var vm = new NotifyDialogViewModel();
            vm.Configure(NotifyDialogViewModel.DialogKind.Tip, message);
            var win = new NotifyWindow { DataContext = vm, Title = "提示" };
            vm.OwnerWindow = win;
            // Tip 非模态,3 秒后自动关闭
            win.Show();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) => { timer.Stop(); win.Close(); };
            timer.Start();
        }), DispatcherPriority.Normal);
    }

    public void ShowError(string message)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            var vm = new NotifyDialogViewModel();
            vm.Configure(NotifyDialogViewModel.DialogKind.Error, message);
            var win = new NotifyWindow { DataContext = vm, Title = "错误" };
            vm.OwnerWindow = win;
            win.ShowDialog();
        }), DispatcherPriority.Normal);
    }

    public bool ShowConfirm(string message)
    {
        if (_dispatcher.CheckAccess())
        {
            return ShowConfirmInternal(message);
        }
        return (bool)_dispatcher.Invoke(new Func<bool>(() => ShowConfirmInternal(message)));
    }

    private static bool ShowConfirmInternal(string message)
    {
        var vm = new NotifyDialogViewModel();
        vm.Configure(NotifyDialogViewModel.DialogKind.Confirm, message);
        var win = new NotifyWindow { DataContext = vm, Title = "确认" };
        vm.OwnerWindow = win;
        win.ShowDialog();
        return vm.Result ?? false;
    }
}
