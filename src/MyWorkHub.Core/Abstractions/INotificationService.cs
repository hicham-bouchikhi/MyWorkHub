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
    /// <paramref name="onActivated"/> is supplied, clicking the toast or its bell-history entry
    /// runs it (e.g. to navigate to the relevant page) — this keeps the callback UI-agnostic so
    /// callers in any layer can wire an action without referencing navigation types.
    /// </summary>
    void Notify(
        string title,
        string message,
        NotificationSeverity severity = NotificationSeverity.INFORMATION,
        Action? onActivated = null);
}
