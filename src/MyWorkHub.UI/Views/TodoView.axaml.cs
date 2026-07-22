using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class TodoView : UserControl
{
    public TodoView()
    {
        InitializeComponent();
    }

    // Load the list when the page comes on screen (the view-model is transient).
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is TodoViewModel vm)
        {
            vm.LoadCommand.Execute(null);
        }
    }
}
