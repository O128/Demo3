using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using 点胶机.ViewModels;

namespace 点胶机.Views.Pages;

public partial class LogView : UserControl
{
    public LogView() => InitializeComponent();

    private void OnDaysChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LogViewModel vm && sender is Selector s && s.SelectedItem is ComboBoxItem item
            && item.Tag is string tagStr && int.TryParse(tagStr, out var days))
        {
            vm.FilterDays = days;
            vm.LoadLogsCommand.Execute(null);
        }
    }
}
