using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace 点胶机.ViewModels;

/// <summary>
/// 弹窗画面 ViewModel —— 支持报警/提示/错误/确认四种样式
/// </summary>
public partial class NotifyDialogViewModel : ObservableObject
{
    /// <summary>对话框类型(决定图标和颜色)</summary>
    public enum DialogKind { Alarm, Tip, Error, Confirm }

    [ObservableProperty] private string _message = "";
    [ObservableProperty] private string _iconChar = "!";
    [ObservableProperty] private Brush _iconBrush = Brushes.Red;
    [ObservableProperty] private Brush _titleBarBrush = Brushes.Red;
    [ObservableProperty] private Visibility _confirmVisible = Visibility.Visible;
    [ObservableProperty] private Visibility _cancelVisible = Visibility.Collapsed;

    /// <summary>确认/取消的结果(DialogResult)</summary>
    public bool? Result { get; private set; }

    /// <summary>所属窗口(用于关闭)</summary>
    public Window? OwnerWindow { get; set; }

    public void Configure(DialogKind kind, string message)
    {
        Message = message;
        switch (kind)
        {
            case DialogKind.Alarm:
                IconChar = "⚠"; IconBrush = Brushes.Red; TitleBarBrush = Brushes.Red;
                ConfirmVisible = Visibility.Visible; CancelVisible = Visibility.Collapsed;
                break;
            case DialogKind.Error:
                IconChar = "✕"; IconBrush = Brushes.Red; TitleBarBrush = Brushes.Red;
                ConfirmVisible = Visibility.Visible; CancelVisible = Visibility.Collapsed;
                break;
            case DialogKind.Tip:
                IconChar = "ℹ"; IconBrush = Brushes.DodgerBlue; TitleBarBrush = Brushes.DodgerBlue;
                ConfirmVisible = Visibility.Visible; CancelVisible = Visibility.Collapsed;
                break;
            case DialogKind.Confirm:
                IconChar = "?"; IconBrush = Brushes.Orange; TitleBarBrush = Brushes.Orange;
                ConfirmVisible = Visibility.Visible; CancelVisible = Visibility.Visible;
                break;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        Result = true;
        OwnerWindow?.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        Result = false;
        OwnerWindow?.Close();
    }
}
