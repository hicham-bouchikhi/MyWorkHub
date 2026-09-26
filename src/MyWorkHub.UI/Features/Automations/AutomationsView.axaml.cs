using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Automations;

namespace MyWorkHub.UI.Features.Automations;

public partial class AutomationsView : UserControl
{
    public AutomationsView()
    {
        InitializeComponent();
    }

    // Loading is triggered by the view appearing, not by the view model's constructor.
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is AutomationsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
