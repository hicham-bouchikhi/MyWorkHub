using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class TeamsView : UserControl
{
    public TeamsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is TeamsViewModel vm && vm.RefreshCommand.CanExecute(null))
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
