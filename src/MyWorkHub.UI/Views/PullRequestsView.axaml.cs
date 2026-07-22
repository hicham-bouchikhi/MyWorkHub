using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class PullRequestsView : UserControl
{
    public PullRequestsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Load once; on later visits the cached view model keeps its rows (and any in-progress
        // review), so we must NOT re-run Refresh, which would rebuild the rows from scratch.
        if (DataContext is PullRequestsViewModel vm && vm.EnsureLoadedCommand.CanExecute(null))
        {
            vm.EnsureLoadedCommand.Execute(null);
        }
    }
}
