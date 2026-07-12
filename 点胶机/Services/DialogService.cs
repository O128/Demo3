using System.Windows;
using System.Windows.Threading;
using 点胶机.Core.Enums;
using 点胶机.Core.Interfaces;
using 点胶机.Services.Toast;
using 点胶机.ViewModels;
using 点胶机.Views.Dialogs;

namespace 点胶机.Services;

/// <summary>
/// 对话框服务实现 —— 报警/提示/错误走 Toast(右下角非模态);确认仍用模态
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Dispatcher _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    private readonly IToastService _toast;

    public DialogService(IToastService toast)
    {
        _toast = toast;
    }

    public void ShowAlarm(int alarmId, string name, AlarmLevel level, string message)
    {
        // 报警映射到 Toast:Stop=错误(红),Pause=警告(黄),Tip=讯息(蓝)
        var title = $"报警 #{alarmId} {name}";
        switch (level)
        {
            case AlarmLevel.Alarm_Stop:
                _toast.Error(title, message);
                break;
            case AlarmLevel.Alarm_Pause:
                _toast.Warning(title, message);
                break;
            default:
                _toast.Info(title, message);
                break;
        }
    }

    public void ShowTip(string message) => _toast.Info("提示", message);

    public void ShowError(string message) => _toast.Error("错误", message);

    public bool ShowConfirm(string message)
    {
        if (_dispatcher.CheckAccess())
            return ShowConfirmInternal(message);
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
