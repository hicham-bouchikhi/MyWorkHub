using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Dashboard;

namespace MyWorkHub.UI.Features.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is DashboardViewModel vm && vm.LoadStatsCommand?.CanExecute(null) == true)
        {
            vm.LoadStatsCommand.Execute(null);
        }
    }
}
