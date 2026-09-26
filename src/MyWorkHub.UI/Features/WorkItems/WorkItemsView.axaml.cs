using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.WorkItems;

namespace MyWorkHub.UI.Features.WorkItems;

public partial class WorkItemsView : UserControl
{
    public WorkItemsView()
    {
        InitializeComponent();
    }

    // Loading is triggered by the view appearing (not by the view model's constructor) so a
    // deep-link focus request issued during navigation is queued before the lists arrive.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is WorkItemsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
