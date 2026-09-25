using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Severity of a user-facing notification, independent of any UI toolkit.</summary>
public enum NotificationSeverity
{
    INFORMATION,
    SUCCESS,
    WARNING,
    ERROR,
}

/// <summary>
/// Surfaces transient, app-wide notifications (toasts) to the user. Implemented in the
/// UI layer; injected into automations and background services so they can report
/// errors and results without referencing any UI types. Implementations must be safe
/// to call from any thread.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a notification with the given title, message and severity. When
    /// <paramref name="target"/> is supplied, clicking the toast or its bell-history entry navigates
    /// there — including to a specific element on the page when <see cref="NavigationTarget.ElementId"/>
    /// is set and the page supports deep links.
    /// </summary>
    void Notify(
        string title,
        string message,
        NotificationSeverity severity = NotificationSeverity.INFORMATION,
        NavigationTarget? target = null);
}
