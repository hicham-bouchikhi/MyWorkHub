using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Settings;

namespace MyWorkHub.UI.Features.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    // Re-read on every visit, so the page shows the configuration as it is now (e.g. after a hand edit).
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
