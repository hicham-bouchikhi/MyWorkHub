using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class WorkItemsView : UserControl
{
    public WorkItemsView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is WorkItemsViewModel vm && vm.RefreshCommand.CanExecute(null))
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
