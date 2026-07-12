using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using 点胶机.Services.Toast;

namespace 点胶机.Views.Dialogs;

/// <summary>右下角 Toast 通知窗口(非模态,可操作主界面)</summary>
public partial class ToastWindow : Window
{
    private readonly ToastService _svc;
    // 筛选用视图
    private readonly ICollectionView _view;

    public ToastWindow(ToastService svc)
    {
        _svc = svc;
        InitializeComponent();
        List.ItemsSource = _svc.Items;

        _view = CollectionViewSource.GetDefaultView(_svc.Items);
        _view.Filter = OnFilter;

        PositionBottomRight();
        svc.Changed += OnSvcChanged;
    }

    /// <summary>筛选函数:按 FilterBox 选中的 Tag 过滤</summary>
    private bool OnFilter(object obj)
    {
        if (obj is not ToastItem t) return false;
        if (_svc.FilterLevel < 0) return true;   // 全部
        return (int)t.Level == _svc.FilterLevel;
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        // InitializeComponent 期间也会触发 SelectionChanged,此时字段未就绪 → 跳过
        if (_svc == null || _view == null) return;
        if (FilterBox?.SelectedItem is not ComboBoxItem item || item.Tag is not string s || !int.TryParse(s, out var lv)) return;
        _svc.FilterLevel = lv;
        _view.Refresh();
    }

    private void OnSvcChanged()
    {
        // 刷新筛选视图
        Dispatcher.BeginInvoke(() => _view.Refresh());
        // 根据可见性显示/隐藏
        Dispatcher.BeginInvoke(() =>
        {
            if (_svc.IsVisible && !IsVisible) { PositionBottomRight(); Show(); }
            else if (!_svc.IsVisible && IsVisible) Hide();
        });
    }

    private void OnClose(object sender, System.Windows.Input.MouseButtonEventArgs e) => _svc.Close();
    private void OnClear(object sender, System.Windows.Input.MouseButtonEventArgs e) => _svc.Clear();

    /// <summary>定位到屏幕右下角(主窗口右侧)</summary>
    private void PositionBottomRight()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 12;
        Top = work.Bottom - Height - 12;
    }
}
