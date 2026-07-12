using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using 点胶机.ViewModels;

namespace 点胶机.Views.Pages;

public partial class AlarmView : UserControl
{
    public AlarmView() => InitializeComponent();

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is AlarmViewModel vm && sender is Selector s && s.SelectedItem is ComboBoxItem item
            && item.Tag is string tagStr && int.TryParse(tagStr, out var days))
        {
            vm.FilterDays = days;
            vm.LoadHistoryCommand.Execute(null);
        }
    }
}
