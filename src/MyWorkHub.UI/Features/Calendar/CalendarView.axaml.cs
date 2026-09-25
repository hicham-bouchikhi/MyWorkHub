using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.Presentation.Features.Calendar;

namespace MyWorkHub.UI.Features.Calendar;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (DataContext is CalendarViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(null);
        }
    }
}
