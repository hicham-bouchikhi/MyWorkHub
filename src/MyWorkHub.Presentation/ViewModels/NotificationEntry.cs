using System.Globalization;
using MyWorkHub.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.UI.ViewModels;

/// <summary>One entry in the notification history (bell flyout). Immutable content; only the
/// read state changes after creation.</summary>
public sealed partial class NotificationEntry : ObservableObject
{
    private readonly Action? _onActivated;

    public NotificationEntry(
        string title,
        string message,
        NotificationSeverity severity,
        DateTime timestamp,
        Action? onActivated = null)
    {
        Title = title;
        Message = message;
        Severity = severity;
        Timestamp = timestamp;
        _onActivated = onActivated;
    }

    /// <summary>True when this entry has an action (clicking it navigates somewhere).</summary>
    public bool IsActionable => _onActivated is not null;

    /// <summary>Runs the entry's action (if any) and marks it read.</summary>
    [RelayCommand]
    private void Activate()
    {
        IsRead = true;
        _onActivated?.Invoke();
    }

    public string Title { get; }

    public string Message { get; }

    public NotificationSeverity Severity { get; }

    public DateTime Timestamp { get; }

    [ObservableProperty]
    private bool _isRead;

    /// <summary>Time-of-day the entry was raised (history is session-only, so no date needed).</summary>
    public string TimeText => Timestamp.ToString("HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Glyph mirroring the app's emoji-icon convention, picked from the severity.</summary>
    public string SeverityGlyph => Severity switch
    {
        NotificationSeverity.SUCCESS => "✅",
        NotificationSeverity.WARNING => "⚠",
        NotificationSeverity.ERROR => "⛔",
        _ => "ℹ",
    };
}
