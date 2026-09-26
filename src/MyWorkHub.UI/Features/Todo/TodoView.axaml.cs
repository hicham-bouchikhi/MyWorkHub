using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Todo;

namespace MyWorkHub.UI.Features.Todo;

public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    // Loading is triggered by the view appearing (not by the view model's constructor) so a
    // deep-link focus request issued during navigation is queued before the list arrives.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is TodoViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
