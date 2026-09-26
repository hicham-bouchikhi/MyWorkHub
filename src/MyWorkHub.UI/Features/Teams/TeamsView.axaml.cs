using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Teams;

namespace MyWorkHub.UI.Features.Teams;

public partial class TeamsView : UserControl
{
    public TeamsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is TeamsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
