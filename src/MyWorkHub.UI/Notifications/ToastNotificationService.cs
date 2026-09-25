using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.UI.Notifications;

/// <summary>
/// Toast implementation of <see cref="INotificationService"/> built on Avalonia's
/// <see cref="WindowNotificationManager"/>. Registered as a singleton; the main window
/// calls <see cref="Attach"/> once it is loaded. All shows are marshalled to the UI
/// thread, so background services and automations can call <see cref="Notify"/> directly.
/// Every notification is also recorded in the <see cref="NotificationCenterViewModel"/>
/// history that backs the top-bar bell.
/// </summary>
public sealed class ToastNotificationService : INotificationService
{
    private readonly NotificationCenterViewModel _center;
    private WindowNotificationManager? _manager;

    public ToastNotificationService(NotificationCenterViewModel center)
    {
        ArgumentNullException.ThrowIfNull(center);
        _center = center;
    }

    /// <summary>Hooks the notification overlay onto the given top-level (the main window).</summary>
    public void Attach(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        _manager = new WindowNotificationManager(topLevel)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 4,
        };
    }

    public void Notify(
        string title,
        string message,
        NotificationSeverity severity = NotificationSeverity.INFORMATION,
        NavigationTarget? target = null)
    {
        // May be called from a background thread (automations); marshal to the UI thread.
        // The history append runs on the same UI-thread post so the bound collection is safe.
        Dispatcher.UIThread.Post(() =>
        {
            var entry = _center.Add(title, message, severity, target);

            // Clicking the toast goes through the history entry, so it navigates AND marks it read.
            Action? onClick = entry.IsActionable ? () => entry.ActivateCommand.Execute(null) : null;
            _manager?.Show(new Notification(title, message, ToNotificationType(severity), onClick: onClick));
        });
    }

    private static NotificationType ToNotificationType(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.SUCCESS => NotificationType.Success,
        NotificationSeverity.WARNING => NotificationType.Warning,
        NotificationSeverity.ERROR => NotificationType.Error,
        _ => NotificationType.Information,
    };
}
