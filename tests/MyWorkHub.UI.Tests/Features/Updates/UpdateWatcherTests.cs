using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Calendar;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.Features.Teams;
using MyWorkHub.Presentation.Features.Updates;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.UI.Tests.Features.AzureDevOps;
using MyWorkHub.UI.Tests.Features.GraphAuth;
using MyWorkHub.UI.Tests.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Updates;

public sealed partial class UpdateWatcherTests
{
    private readonly FakeGraphConnection _graph = new();
    private readonly RecordingNotificationService _notifications = new();
    private readonly FakeAzureDevOpsConnection _azureDevOps = new();

    [Fact]
    public async Task Should_notify_once_per_email_that_arrives_after_the_first_check()
    {
        var email = new FakeEmailService(_graph, FakeEmailService.Item("old"));
        var watcher = new UpdateWatcher(_notifications, email: email);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        email.Emails.Insert(0, FakeEmailService.Item("new"));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("New email from Alice", sent.Title);
        Assert.Equal("Subject new", sent.Message);
        Assert.Equal(EmailViewModel.TargetFor("new"), sent.Target);
    }

    [Fact]
    public async Task Should_not_notify_for_an_email_that_is_already_read()
    {
        var email = new FakeEmailService(_graph);
        var watcher = new UpdateWatcher(_notifications, email: email);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        email.Emails.Add(FakeEmailService.Item("read") with { IsRead = true });
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Should_notify_when_a_chat_gets_a_new_unread_message()
    {
        var teams = new FakeTeamsService(_graph, Chat("c1", minute: 0, isUnread: false), Chat("c2", minute: 0, isUnread: false));
        var watcher = new UpdateWatcher(_notifications, teams: teams);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        teams.Chats[1] = Chat("c2", minute: 5, isUnread: true);
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("New Teams message from Bob", sent.Title);
        Assert.Equal("Chat c2: Hello at 5", sent.Message);
        Assert.Equal(new NavigationTarget(typeof(TeamsViewModel)), sent.Target);
    }

    [Fact]
    public async Task Should_not_notify_again_for_a_chat_whose_latest_message_did_not_change()
    {
        var teams = new FakeTeamsService(_graph, Chat("c1", minute: 0, isUnread: true));
        var watcher = new UpdateWatcher(_notifications, teams: teams);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_notifications.Sent);
    }

[Fact]
    public async Task Should_notify_for_a_new_pull_request_to_review_but_not_for_my_own()
    {
        var devOps = new FakeAzureDevOpsService(_azureDevOps);
        var watcher = new UpdateWatcher(_notifications, azureDevOps: devOps);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        devOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(7));
        devOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(8, PullRequestRole.AUTHOR));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("Pull request to review from Alice", sent.Title);
        Assert.Equal("!7 PR 7 (web)", sent.Message);
        Assert.Equal(PullRequestsViewModel.TargetFor(7), sent.Target);
    }

    [Fact]
    public async Task Should_notify_for_a_newly_assigned_work_item()
    {
        var devOps = new FakeAzureDevOpsService(_azureDevOps);
        var watcher = new UpdateWatcher(_notifications, azureDevOps: devOps);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        devOps.WorkItems.Add(FakeAzureDevOpsService.WorkItem(42));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent, n => n.Target == WorkItemsViewModel.TargetForWorkItem(42));
        Assert.Equal("Work item assigned: Task 42", sent.Title);
        Assert.Equal("Item 42", sent.Message);
    }

    [Fact]
    public async Task Should_notify_for_a_new_mention_that_was_not_opened_yet()
    {
        var devOps = new FakeAzureDevOpsService(_azureDevOps);
        devOps.WorkItems.Add(FakeAzureDevOpsService.WorkItem(1));
        var seen = new FakeSeenMentionRepository();
        seen.Seen.Add(11);
        var watcher = new UpdateWatcher(_notifications, azureDevOps: devOps, seenMentions: seen);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        devOps.Mentions.Add(FakeAzureDevOpsService.Mention(10));
        devOps.Mentions.Add(FakeAzureDevOpsService.Mention(11));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("Bob mentioned you", sent.Title);
        Assert.Equal("Item 1: @Jane Doe have a look", sent.Message);
        Assert.Equal(WorkItemsViewModel.TargetForMention(10), sent.Target);
    }

    [Fact]
    public async Task Should_notify_for_a_new_calendar_event_in_the_coming_week()
    {
        var calendar = new FakeCalendarService(_graph);
        var watcher = new UpdateWatcher(_notifications, calendar: calendar);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        calendar.Events.Add(new CalendarEvent("e1", "Sprint review", new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc), IsAllDay: false, "Room 4"));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("New calendar event: Sprint review", sent.Title);
        Assert.Equal(7, calendar.RequestedDays);
        Assert.Equal(new NavigationTarget(typeof(CalendarViewModel)), sent.Target);
    }

    [Fact]
    public async Task Should_keep_checking_the_other_sources_when_one_source_fails()
    {
        var email = new FakeEmailService(_graph);
        var devOps = new FakeAzureDevOpsService(_azureDevOps);
        var watcher = new UpdateWatcher(_notifications, email: email, azureDevOps: devOps);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        _graph.IsSignedIn = false;
        devOps.MentionsFailure = new HttpRequestException("400 preview API");
        devOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(7));
        devOps.WorkItems.Add(FakeAzureDevOpsService.WorkItem(42));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [PullRequestsViewModel.TargetFor(7), WorkItemsViewModel.TargetForWorkItem(42)],
            _notifications.Sent.Select(n => n.Target));
    }

    [Fact]
    public async Task Should_stop_when_cancelled()
    {
        var email = new FakeEmailService(_graph);
        var watcher = new UpdateWatcher(_notifications, email: email);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        email.Failure = new OperationCanceledException(cancelled.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => watcher.CheckAsync(cancelled.Token));
    }

    [Fact]
    public async Task Should_neither_query_nor_notify_a_source_that_is_switched_off()
    {
        var email = new FakeEmailService(_graph);
        var settings = new FakeSettingsService { Notifications = NotificationSettings.Default with { Emails = false } };
        var devOps = new FakeAzureDevOpsService(_azureDevOps);
        var watcher = new UpdateWatcher(_notifications, email: email, azureDevOps: devOps, settings: settings);

        await watcher.CheckAsync(TestContext.Current.CancellationToken);
        email.Emails.Add(FakeEmailService.Item("ignored"));
        devOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(7));
        await watcher.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal([PullRequestsViewModel.TargetFor(7)], _notifications.Sent.Select(n => n.Target));
    }

    [Fact]
    public async Task Should_start_from_a_fresh_baseline_when_a_source_is_switched_back_on()
    {
        var email = new FakeEmailService(_graph);
        var settings = new FakeSettingsService();
        var watcher = new UpdateWatcher(_notifications, email: email, settings: settings);
        var ct = TestContext.Current.CancellationToken;

        await watcher.CheckAsync(ct);
        settings.Notifications = NotificationSettings.Default with { Emails = false };
        await watcher.CheckAsync(ct);
        email.Emails.Add(FakeEmailService.Item("while-off"));
        settings.Notifications = NotificationSettings.Default;
        await watcher.CheckAsync(ct);
        email.Emails.Add(FakeEmailService.Item("after"));
        await watcher.CheckAsync(ct);

        Assert.Equal([EmailViewModel.TargetFor("after")], _notifications.Sent.Select(n => n.Target));
    }

    [Fact]
    public async Task Should_check_at_start_then_wait_the_configured_interval_read_again_before_every_wait()
    {
        var email = new FakeEmailService(_graph);
        var settings = new FakeSettingsService { Notifications = NotificationSettings.Default with { RefreshIntervalMinutes = 10 } };
        using var stop = new CancellationTokenSource();
        List<TimeSpan> waits = [];
        var watcher = new UpdateWatcher(_notifications, email: email, settings: settings)
        {
            Delay = async (wait, token) =>
            {
                waits.Add(wait);
                if (waits.Count == 1)
                {
                    email.Emails.Add(FakeEmailService.Item("new"));
                    settings.Notifications = settings.Notifications with { RefreshIntervalMinutes = 3 };
                    return;
                }

                await stop.CancelAsync();
                token.ThrowIfCancellationRequested();
            },
        };

        await watcher.RunAsync(stop.Token);

        Assert.Equal([TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3)], waits);
        Assert.Equal([EmailViewModel.TargetFor("new")], _notifications.Sent.Select(n => n.Target));
    }

    [Fact]
    public async Task Should_refresh_the_page_being_shown_after_each_periodic_check()
    {
        var page = new RefreshCountingPage();
        var navigation = new FakeNavigationService { CurrentPage = page };
        using var stop = new CancellationTokenSource();
        var calls = 0;
        var watcher = new UpdateWatcher(_notifications, navigation: navigation)
        {
            Delay = async (_, token) =>
            {
                if (++calls == 3)
                {
                    await stop.CancelAsync();
                    token.ThrowIfCancellationRequested();
                }
            },
        };

        await watcher.RunAsync(stop.Token);

        Assert.Equal(2, page.Refreshes);
    }

    private sealed partial class RefreshCountingPage() : PageViewModel("Refreshable"), IRefreshablePage
    {
        public int Refreshes { get; private set; }

        [RelayCommand]
        private Task RefreshAsync()
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }

        private static TeamsChatItem Chat(string id, int minute, bool isUnread)
        => new(id, "Chat " + id, "Hello at " + minute, "Bob",
            new DateTime(2026, 9, 29, 9, minute, 0, DateTimeKind.Utc), isUnread, "");
}
