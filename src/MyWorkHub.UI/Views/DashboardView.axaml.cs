using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class DashboardView : UserControl
{
    // The auto-refresh timer lives only while the dashboard is on screen: started when the view
    // loads, stopped when it unloads. Owning it here (rather than in the view model) keeps the view
    // model free of any Avalonia/UI-thread dependency so it can be reused by another front-end.
    private DispatcherTimer? _timer;

    public DashboardView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }

        _timer ??= CreateTimer(vm.RefreshIntervalMinutes);
        _timer.Start();
        TriggerRefresh();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _timer?.Stop();
    }

    private DispatcherTimer CreateTimer(int intervalMinutes)
    {
        var timer = new DispatcherTimer { Interval = System.TimeSpan.FromMinutes(intervalMinutes) };
        timer.Tick += (_, _) => TriggerRefresh();
        return timer;
    }

    private void TriggerRefresh()
    {
        if (DataContext is DashboardViewModel vm && vm.RefreshCommand.CanExecute(null))
        {
            vm.RefreshCommand.Execute(null);
        }
    }
}
