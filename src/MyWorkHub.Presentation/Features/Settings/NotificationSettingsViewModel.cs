using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → Notifications: how often the app looks for new items and which sources raise a notification.
/// Every change is persisted at once (no Save button); the update check re-reads it before its next wait.
/// </summary>
public sealed partial class NotificationSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService? _settings;
    private bool _isLoading;

    public NotificationSettingsViewModel(ISettingsService? settings = null)
    {
        _settings = settings;
    }

    /// <summary>False when settings cannot be persisted; the panel then shows a notice.</summary>
    public bool IsAvailable => _settings is not null;

    public static decimal MinRefreshIntervalMinutes => NotificationSettings.MinRefreshIntervalMinutes;

    public static decimal MaxRefreshIntervalMinutes => NotificationSettings.MaxRefreshIntervalMinutes;

    /// <summary>Minutes between two checks (decimal because that is what a numeric input edits).</summary>
    [ObservableProperty]
    private decimal _refreshIntervalMinutes = NotificationSettings.Default.RefreshIntervalMinutes;

    [ObservableProperty]
    private bool _emails = true;

    [ObservableProperty]
    private bool _teamsChats = true;

    [ObservableProperty]
    private bool _pullRequests = true;

    [ObservableProperty]
    private bool _workItems = true;

    [ObservableProperty]
    private bool _mentions = true;

    [ObservableProperty]
    private bool _calendarEvents = true;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>The most recent save (completed when idle); lets tests await the fire-on-change persistence.</summary>
    internal Task LastSave { get; private set; } = Task.CompletedTask;

    /// <summary>Shows the persisted values without re-saving them.</summary>
    public void Load()
    {
        if (_settings is not null)
        {
            Show(_settings.GetNotifications());
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPropertyChanged(e);
        if (_isLoading || _settings is null || e.PropertyName is nameof(ErrorMessage))
        {
            return;
        }

        var settings = new NotificationSettings(
            (int)Math.Round(RefreshIntervalMinutes),
            Emails,
            TeamsChats,
            PullRequests,
            WorkItems,
            Mentions,
            CalendarEvents).Normalize();
        Show(settings);
        LastSave = PersistAsync(_settings, settings);
    }

    private void Show(NotificationSettings settings)
    {
        _isLoading = true;
        try
        {
            RefreshIntervalMinutes = settings.RefreshIntervalMinutes;
            Emails = settings.Emails;
            TeamsChats = settings.TeamsChats;
            PullRequests = settings.PullRequests;
            WorkItems = settings.WorkItems;
            Mentions = settings.Mentions;
            CalendarEvents = settings.CalendarEvents;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task PersistAsync(ISettingsService settings, NotificationSettings notifications)
    {
        try
        {
            await settings.SaveNotificationsAsync(notifications);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not be saved: {ex.Message}";
        }
    }
}
