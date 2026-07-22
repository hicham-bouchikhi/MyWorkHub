using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.NetworkInformation;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Landing page that aggregates at-a-glance counts and previews from every data source.
/// Each source is wrapped in its own try/catch so a single failure (or a not-yet-registered
/// optional service such as <see cref="ITeamsService"/>) never breaks the dashboard — it
/// degrades to a zero/empty widget and the offline indicator is raised instead.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    // The transport job's run history feeds the automation widget once Phase 9/10 wires
    // an IAutomationLogger. Until then the logger is null and the widget shows defaults.
    private const string TRANSPORT_JOB_NAME = "TransportReimbursementJob";

    private readonly INavigationService _navigation;
    private readonly IEmailService? _email;
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly IAzureDevOpsSettingsService? _azdoSettings;
    private readonly ITeamsService? _teams;
    private readonly ITodoRepository? _todo;
    private readonly IAutomationLogger? _automationLogger;
    private readonly ILogger<DashboardViewModel>? _logger;

    [ObservableProperty]
    private int _unreadEmailCount;

    [ObservableProperty]
    private bool _hasImportantEmails;

    [ObservableProperty]
    private int _teamsUnreadCount;

    [ObservableProperty]
    private int _pullRequestCount;

    [ObservableProperty]
    private int _workItemCount;

    [ObservableProperty]
    private int _workItemsAttentionCount;

    [ObservableProperty]
    private string _workItemsOverdueText = "";

    [ObservableProperty]
    private int _todoDueTodayCount;

    [ObservableProperty]
    private string _nextAutomationRun = "Not scheduled";

    [ObservableProperty]
    private string _lastAutomationStatus = "No runs yet";

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private string _offlineMessage = "";

    [ObservableProperty]
    private string _statusText = "Not yet refreshed";

    public DashboardViewModel(
        INavigationService navigation,
        IEmailService? email = null,
        IAzureDevOpsService? azureDevOps = null,
        ITeamsService? teams = null,
        ITodoRepository? todo = null,
        IAutomationLogger? automationLogger = null,
        IOptions<UiOptions>? uiOptions = null,
        ILogger<DashboardViewModel>? logger = null,
        IAzureDevOpsSettingsService? azdoSettings = null)
        : base("Dashboard")
    {
        ArgumentNullException.ThrowIfNull(navigation);

        _navigation = navigation;
        _email = email;
        _azureDevOps = azureDevOps;
        _azdoSettings = azdoSettings;
        _teams = teams;
        _todo = todo;
        _automationLogger = automationLogger;
        _logger = logger;

        var interval = uiOptions?.Value.RefreshIntervalMinutes ?? 5;
        RefreshIntervalMinutes = interval > 0 ? interval : 5;
    }

    /// <summary>The auto-refresh cadence in minutes, read by the view that owns the refresh timer.</summary>
    public int RefreshIntervalMinutes { get; }

    /// <summary>Top important (unread/flagged) emails — preview list for the email widget.</summary>
    public ObservableCollection<EmailItem> TopEmails { get; } = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;

        // Teams failures never count against dashboard health — tracked separately.
        var failed = new List<string>();

        try
        {
            if (!await TryRefreshEmailAsync().ConfigureAwait(true))
                failed.Add("Email");
            await TryRefreshTeamsAsync().ConfigureAwait(true);
            if (!await TryRefreshAzureDevOpsAsync().ConfigureAwait(true))
                failed.Add("Azure DevOps");
            if (!await TryRefreshTodoAsync().ConfigureAwait(true))
                failed.Add("Todo");
            if (!await TryRefreshAutomationAsync().ConfigureAwait(true))
                failed.Add("Automation");
        }
        finally
        {
            IsRefreshing = false;
        }

        var now = DateTime.Now;
        var noNetwork = !NetworkInterface.GetIsNetworkAvailable();
        IsOffline = noNetwork || failed.Count > 0;
        OfflineMessage = noNetwork
            ? "⚠ No network connection — showing last fetched data."
            : failed.Count > 0
                ? $"⚠ {string.Join(", ", failed)} unavailable — showing last fetched data."
                : "";
        StatusText = IsOffline
            ? $"Offline — last update {now.ToString("HH:mm", CultureInfo.CurrentCulture)}"
            : $"Last updated {now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}";
    }

    /// <returns><c>true</c> on success or when the service is unavailable; <c>false</c> on failure.</returns>
    private async Task<bool> TryRefreshEmailAsync()
    {
        if (_email is null)
        {
            return true;
        }

        try
        {
            UnreadEmailCount = await _email.GetUnreadCountAsync().ConfigureAwait(true);

            var important = await _email.GetImportantEmailsAsync().ConfigureAwait(true);
            TopEmails.Clear();
            foreach (var item in important.Take(3))
            {
                TopEmails.Add(item);
            }

            HasImportantEmails = TopEmails.Count > 0;
            return true;
        }
        catch (Exception ex)
        {
            LogRefreshFailure("email", ex);
            return false;
        }
    }

    private async Task TryRefreshTeamsAsync()
    {
        if (_teams is null)
        {
            return;
        }

        try
        {
            TeamsUnreadCount = await _teams.GetUnreadCountAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // The dashboard must never fail because of Teams (ARCHITECTURE.md / T027): show 0.
            LogRefreshFailure("Teams", ex);
            TeamsUnreadCount = 0;
        }
    }

    private async Task<bool> TryRefreshAzureDevOpsAsync()
    {
        if (_azureDevOps is null)
        {
            return true;
        }

        if (_azdoSettings is { } s)
        {
            var cfg = s.Get();
            if (!cfg.HasPersonalAccessToken || cfg.Projects.Count == 0)
                return true;
        }

        try
        {
            var pullRequests = await _azureDevOps.GetPullRequestsForReviewAsync().ConfigureAwait(true);
            PullRequestCount = pullRequests.Count;

            var today = DateTime.Today;
            var workItems = await _azureDevOps.GetMyWorkItemsAsync().ConfigureAwait(true);
            var overdue = workItems.Count(w => w.DueDate is { } due && due.Date <= today);

            // Work items rarely carry a due date in Agile/Scrum, so the headline number is the
            // total assigned in the current sprint; overdue (if any) is shown as a sub-line.
            WorkItemCount = workItems.Count;
            WorkItemsAttentionCount = overdue;
            WorkItemsOverdueText = overdue > 0 ? $"{overdue} overdue" : "";
            return true;
        }
        catch (Exception ex)
        {
            LogRefreshFailure("Azure DevOps", ex);
            return false;
        }
    }

    private async Task<bool> TryRefreshTodoAsync()
    {
        if (_todo is null)
        {
            return true;
        }

        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var items = await _todo.GetAllAsync().ConfigureAwait(true);
            TodoDueTodayCount = items.Count(t => !t.IsCompleted && t.DueDate is { } due && due <= today);
            return true;
        }
        catch (Exception ex)
        {
            LogRefreshFailure("todo", ex);
            return false;
        }
    }

    private async Task<bool> TryRefreshAutomationAsync()
    {
        if (_automationLogger is null)
        {
            return true;
        }

        try
        {
            var history = await _automationLogger.GetHistoryAsync(TRANSPORT_JOB_NAME, 1).ConfigureAwait(true);
            var last = history.Count > 0 ? history[0] : null;
            LastAutomationStatus = last is null
                ? "No runs yet"
                : $"{last.Status} · {last.StartedAt.ToString("g", CultureInfo.CurrentCulture)}";
            return true;
        }
        catch (Exception ex)
        {
            LogRefreshFailure("automation history", ex);
            return false;
        }
    }

    private void LogRefreshFailure(string source, Exception exception)
    {
        if (_logger is not null)
        {
            LogRefreshFailureCore(_logger, source, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dashboard: {Source} unavailable; widget left at its last value.")]
    private static partial void LogRefreshFailureCore(ILogger logger, string source, Exception exception);

    [RelayCommand]
    private void OpenEmail() => _navigation.NavigateTo(typeof(EmailViewModel));

    [RelayCommand]
    private void OpenTeams() => _navigation.NavigateTo(typeof(TeamsViewModel));

    [RelayCommand]
    private void OpenPullRequests() => _navigation.NavigateTo(typeof(PullRequestsViewModel));

    [RelayCommand]
    private void OpenWorkItems() => _navigation.NavigateTo(typeof(WorkItemsViewModel));

    [RelayCommand]
    private void OpenTodo() => _navigation.NavigateTo(typeof(TodoViewModel));

    [RelayCommand]
    private void OpenAutomations() => _navigation.NavigateTo(typeof(AutomationsViewModel));
}
