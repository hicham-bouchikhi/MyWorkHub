using System.Globalization;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.Presentation.ViewModels;

/// <summary>One entry in the notification history (bell flyout). Immutable content; only the
/// read state changes after creation.</summary>
public sealed partial class NotificationEntry : ObservableObject
{
    private readonly INavigationService _navigation;

    public NotificationEntry(
        string title,
        string message,
        NotificationSeverity severity,
        DateTime timestamp,
        INavigationService navigation,
        NavigationTarget? target = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        Title = title;
        Message = message;
        Severity = severity;
        Timestamp = timestamp;
        Target = target;
        _navigation = navigation;
    }

    /// <summary>Where activating this entry navigates to, or null when it is informational only.</summary>
    public NavigationTarget? Target { get; }

    /// <summary>True when this entry has a destination (clicking it navigates somewhere).</summary>
    public bool IsActionable => Target is not null;

    /// <summary>Marks the entry read and navigates to its <see cref="Target"/> (if any).</summary>
    [RelayCommand]
    private void Activate()
    {
        IsRead = true;
        if (Target is not null)
        {
            _navigation.NavigateTo(Target);
        }
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
