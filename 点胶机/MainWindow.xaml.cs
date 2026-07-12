using System.Windows.Controls;
using MahApps.Metro.Controls;
using 点胶机.ViewModels;

namespace 点胶机;

/// <summary>
/// 启动壳(MainWindow)—— 顶部状态条 + 左导航 + 中间 ContentControl
/// </summary>
public partial class MainWindow : MetroWindow
{
    private readonly ShellViewModel _viewModel;

    public MainWindow(ShellViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        // 导航列表选择 → 切换页面
        NavList.SelectionChanged += OnNavChanged;
    }

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedIndex < 0) return;
        switch (NavList.SelectedIndex)
        {
            case 0: _viewModel.GoHomeCommand.Execute(null); break;
            case 1: _viewModel.GoManualCommand.Execute(null); break;
            case 2: _viewModel.GoAutoCommand.Execute(null); break;
            case 3: _viewModel.GoRecipeCommand.Execute(null); break;
            case 4: _viewModel.GoAlarmCommand.Execute(null); break;
            case 5: _viewModel.GoLogCommand.Execute(null); break;
            case 6: _viewModel.GoIoCommand.Execute(null); break;
            case 7: _viewModel.GoSettingCommand.Execute(null); break;
        }
    }
}
