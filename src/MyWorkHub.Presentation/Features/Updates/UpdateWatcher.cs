using System.Globalization;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Calendar;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.Features.Teams;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.Presentation.Navigation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MyWorkHub.Presentation.Features.Updates;

/// <summary>
/// Looks for items that arrived since the previous check and raises one notification per new item. The first
/// check of a source only records what is already there, so launching the app does not replay the inbox.
/// Each source is checked on its own: one that is signed out or failing is skipped (and retried next check)
/// without holding back the others. A source switched off in the settings is not queried at all, and starts
/// from a fresh baseline when switched back on. <see cref="RunAsync"/> repeats the check every
/// <c>UI:RefreshIntervalMinutes</c> (re-read before every wait) and then refreshes the page being shown.
/// </summary>
public sealed partial class UpdateWatcher
{
    private const int CALENDAR_DAYS = 7;

    private readonly INotificationService _notifications;
    private readonly IEmailService? _email;
    private readonly ITeamsService? _teams;
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly ISeenMentionRepository? _seenMentions;
    private readonly ICalendarService? _calendar;
    private readonly ISettingsService? _settings;
    private readonly INavigationService? _navigation;
    private readonly ILogger<UpdateWatcher> _logger;

    private readonly KnownKeys<string> _emails = new();
    private readonly KnownKeys<(string ChatId, DateTime? LastMessageAt)> _chatMessages = new();
    private readonly KnownKeys<int> _pullRequests = new();
    private readonly KnownKeys<int> _workItems = new();
    private readonly KnownKeys<int> _mentions = new();
    private readonly KnownKeys<string> _events = new();

    public UpdateWatcher(
        INotificationService notifications,
        IEmailService? email = null,
        ITeamsService? teams = null,
        IAzureDevOpsService? azureDevOps = null,
        ISeenMentionRepository? seenMentions = null,
        ICalendarService? calendar = null,
        ISettingsService? settings = null,
        INavigationService? navigation = null,
        ILogger<UpdateWatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        _notifications = notifications;
        _email = email;
        _teams = teams;
        _azureDevOps = azureDevOps;
        _seenMentions = seenMentions;
        _calendar = calendar;
        _settings = settings;
        _navigation = navigation;
        _logger = logger ?? NullLogger<UpdateWatcher>.Instance;
    }

    /// <summary>Waits between two checks; replaceable so tests need not wait for real.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    /// <summary>Checks once at start (the baseline), then every refresh interval until cancelled.</summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        try
        {
            await CheckAsync(ct);
            while (true)
            {
                await Delay(TimeSpan.FromMinutes(CurrentSettings().RefreshIntervalMinutes), ct);
                await CheckAsync(ct);
                await RefreshShownPageAsync();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The app is shutting down.
        }
    }

    /// <summary>Checks every switched-on source once.</summary>
    public async Task CheckAsync(CancellationToken ct = default)
    {
        var settings = CurrentSettings();

        if (_email is not null && Enabled(settings.Emails, _emails))
        {
            await GuardAsync("emails", () => CheckEmailsAsync(_email, ct), ct);
        }

        if (_teams is not null && Enabled(settings.TeamsChats, _chatMessages))
        {
            await GuardAsync("Teams chats", () => CheckChatsAsync(_teams, ct), ct);
        }

        if (_azureDevOps is not null)
        {
            if (Enabled(settings.PullRequests, _pullRequests))
            {
                await GuardAsync("pull requests", () => CheckPullRequestsAsync(_azureDevOps, ct), ct);
            }

            var workItemsOn = Enabled(settings.WorkItems, _workItems);
            var mentionsOn = Enabled(settings.Mentions, _mentions);
            if (workItemsOn || mentionsOn)
            {
                await GuardAsync("work items", () => CheckWorkItemsAsync(_azureDevOps, workItemsOn, mentionsOn, ct), ct);
            }
        }

        if (_calendar is not null && Enabled(settings.CalendarEvents, _events))
        {
            await GuardAsync("calendar events", () => CheckEventsAsync(_calendar, ct), ct);
        }
    }

    private NotificationSettings CurrentSettings() => _settings?.GetNotifications() ?? NotificationSettings.Default;

    private static bool Enabled<TKey>(bool isOn, KnownKeys<TKey> known)
    {
        if (!isOn)
        {
            known.Reset();
        }

        return isOn;
    }

    private async Task RefreshShownPageAsync()
    {
        if (_navigation?.CurrentPage is IRefreshablePage { RefreshCommand: { IsRunning: false } refresh })
        {
            try
            {
                await refresh.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                LogCheckFailed(_logger, "the page being shown", ex);
            }
        }
    }

    private async Task GuardAsync(string source, Func<Task> check, CancellationToken ct)
    {
        try
        {
            await check();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCheckFailed(_logger, source, ex);
        }
    }

    private async Task CheckEmailsAsync(IEmailService service, CancellationToken ct)
    {
        var emails = await service.GetRecentEmailsAsync(ct);
        foreach (var email in _emails.Fresh(emails, e => e.Id).Where(e => !e.IsRead))
        {
            _notifications.Notify($"New email from {email.From}", email.Subject, target: EmailViewModel.TargetFor(email.Id));
        }
    }

    private async Task CheckChatsAsync(ITeamsService service, CancellationToken ct)
    {
        var chats = await service.GetRecentChatsAsync(ct);
        foreach (var chat in _chatMessages.Fresh(chats, c => (c.Id, c.LastMessageAt)).Where(c => c.IsUnread))
        {
            _notifications.Notify(
                $"New Teams message from {chat.LastSenderName}",
                $"{chat.Title}: {chat.LastMessagePreview}",
                target: new NavigationTarget(typeof(TeamsViewModel)));
        }
    }

    private async Task CheckPullRequestsAsync(IAzureDevOpsService service, CancellationToken ct)
    {
        var pullRequests = await service.GetMyPullRequestsAsync(ct);
        foreach (var pr in _pullRequests.Fresh(pullRequests, p => p.Id).Where(p => p.Role == PullRequestRole.REVIEWER))
        {
            _notifications.Notify(
                $"Pull request to review from {pr.Author}",
                string.Create(CultureInfo.InvariantCulture, $"!{pr.Id} {pr.Title} ({pr.Repository})"),
                target: PullRequestsViewModel.TargetFor(pr.Id));
        }
    }

    private async Task CheckWorkItemsAsync(IAzureDevOpsService service, bool workItemsOn, bool mentionsOn, CancellationToken ct)
    {
        var workItems = await service.GetMyWorkItemsAsync(ct);
        foreach (var item in workItemsOn ? _workItems.Fresh(workItems, w => w.Id) : [])
        {
            _notifications.Notify(
                string.Create(CultureInfo.InvariantCulture, $"Work item assigned: {item.Type} {item.Id}"),
                item.Title,
                target: WorkItemsViewModel.TargetForWorkItem(item.Id));
        }

        if (mentionsOn)
        {
            await GuardAsync("work item mentions", () => CheckMentionsAsync(service, workItems, ct), ct);
        }
    }

    private async Task CheckMentionsAsync(IAzureDevOpsService service, IReadOnlyList<WorkItem> workItems, CancellationToken ct)
    {
        var mentions = await service.GetWorkItemMentionsAsync(workItems, ct);
        var fresh = _mentions.Fresh(mentions, m => m.CommentId);
        if (fresh.Count == 0)
        {
            return;
        }

        var opened = _seenMentions is null ? new HashSet<int>() : await _seenMentions.GetSeenCommentIdsAsync(ct);
        foreach (var mention in fresh.Where(m => !opened.Contains(m.CommentId)))
        {
            _notifications.Notify(
                $"{mention.AuthorDisplayName} mentioned you",
                $"{mention.WorkItemTitle}: {mention.TextSnippet}",
                target: WorkItemsViewModel.TargetForMention(mention.CommentId));
        }
    }

    private async Task CheckEventsAsync(ICalendarService service, CancellationToken ct)
    {
        var events = await service.GetUpcomingEventsAsync(CALENDAR_DAYS, ct);
        foreach (var calendarEvent in _events.Fresh(events, e => e.Id))
        {
            _notifications.Notify(
                $"New calendar event: {calendarEvent.Subject}",
                calendarEvent.Start.ToLocalTime().ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture),
                target: new NavigationTarget(typeof(CalendarViewModel)));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped {Source} during the update check")]
    private static partial void LogCheckFailed(ILogger logger, string source, Exception exception);

    /// <summary>
    /// Keys already seen for one source. Known keys accumulate, so an item that drops out of a capped list and
    /// comes back is not reported twice.
    /// </summary>
    private sealed class KnownKeys<TKey>
    {
        private HashSet<TKey>? _known;

        /// <summary>Forgets every key, so the next <see cref="Fresh"/> call is a new baseline.</summary>
        public void Reset() => _known = null;

        /// <summary>The items whose key was not seen before; none on the very first call (the baseline).</summary>
        public List<T> Fresh<T>(IReadOnlyList<T> items, Func<T, TKey> key)
        {
            if (_known is null)
            {
                _known = [.. items.Select(key)];
                return [];
            }

            var known = _known;
            return [.. items.Where(item => known.Add(key(item)))];
        }
    }
}
