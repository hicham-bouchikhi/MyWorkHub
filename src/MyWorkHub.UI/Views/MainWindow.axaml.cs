using Avalonia.Controls;
using Avalonia.Interactivity;
using MyWorkHub.UI.Notifications;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Views;

public partial class MainWindow : Window
{
    private readonly ToastNotificationService? _notifications;

    // Parameterless constructor for the Avalonia designer / XAML loader.
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(ToastNotificationService notifications)
        : this()
    {
        _notifications = notifications;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The toast overlay can only attach once the window (top-level) is loaded.
        _notifications?.Attach(this);
    }

    // Opening the bell flyout counts as seeing the notifications, so clear the unread badge.
    private void OnNotificationsFlyoutOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Notifications.MarkAllReadCommand.Execute(null);
        }
    }
}
