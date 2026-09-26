using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.PullRequests;

namespace MyWorkHub.UI.Features.PullRequests;

public partial class PullRequestsView : UserControl
{
    public PullRequestsView()
    {
        InitializeComponent();
    }

    // Loading is triggered by the view appearing (not by the view model's constructor) so a
    // deep-link focus request issued during navigation is queued before the list arrives.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is PullRequestsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
