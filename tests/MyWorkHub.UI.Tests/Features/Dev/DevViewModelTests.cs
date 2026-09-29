using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Presentation.Features.Dev;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.Features.Todo;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Features.Dev;
using MyWorkHub.UI.Tests.Features.AzureDevOps;
using MyWorkHub.UI.Tests.Features.GraphAuth;
using MyWorkHub.UI.Tests.Features.Todo;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests.Features.Dev;

public sealed class DevViewModelTests
{
    private readonly RecordingNotificationService _notifications = new();

    // --- Severity test notifications ------------------------------------------------

    [Theory]
    [InlineData(NotificationSeverity.INFORMATION)]
    [InlineData(NotificationSeverity.SUCCESS)]
    [InlineData(NotificationSeverity.WARNING)]
    [InlineData(NotificationSeverity.ERROR)]
    public void Should_send_a_test_notification_of_the_requested_severity_without_a_target(NotificationSeverity severity)
    {
        var viewModel = new DevViewModel(_notifications);

        viewModel.SendTestNotificationCommand.Execute(severity);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal(severity, sent.Severity);
        Assert.Null(sent.Target);
    }

    [Fact]
    public void Should_list_every_severity_so_the_page_offers_one_button_each()
    {
        Assert.Equal(Enum.GetValues<NotificationSeverity>(), DevViewModel.Severities);
    }

    [Fact]
    public void Should_disable_every_command_when_no_notification_service_is_registered()
    {
        var viewModel = new DevViewModel();

        Assert.False(viewModel.CanNotify);
        Assert.False(viewModel.SendTestNotificationCommand.CanExecute(NotificationSeverity.INFORMATION));
        Assert.False(viewModel.SendTodoDeepLinkCommand.CanExecute(null));
        Assert.False(viewModel.SendEmailDeepLinkCommand.CanExecute(null));
        Assert.False(viewModel.SendPullRequestDeepLinkCommand.CanExecute(null));
        Assert.False(viewModel.SendWorkItemDeepLinkCommand.CanExecute(null));
        Assert.False(viewModel.SendMentionDeepLinkCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_send_the_delayed_test_notification_once_the_delay_has_elapsed()
    {
        var viewModel = new DevViewModel(_notifications) { DelaySeconds = 0 };

        await viewModel.SendDelayedTestNotificationCommand.ExecuteAsync(null);

        var sent = Assert.Single(_notifications.Sent);
        Assert.Equal("Delayed test notification", sent.Title);
        Assert.Equal(NotificationSeverity.INFORMATION, sent.Severity);
    }

    [Fact]
    public async Task Should_not_send_the_delayed_test_notification_when_cancelled_during_the_delay()
    {
        var viewModel = new DevViewModel(_notifications) { DelaySeconds = 60 };

        var pending = viewModel.SendDelayedTestNotificationCommand.ExecuteAsync(null);
        viewModel.SendDelayedTestNotificationCancelCommand.Execute(null);
        await pending;

        Assert.Empty(_notifications.Sent);
    }

    // --- Deep-link test notifications: real rows ------------------------------------

    [Fact]
    public async Task Should_target_the_newest_real_todo_row()
    {
        var newest = FakeTodoRepository.Item("Newest");
        var viewModel = new DevViewModel(_notifications, todos: new FakeTodoRepository(newest, FakeTodoRepository.Item("Older")));

        await viewModel.SendTodoDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(TodoViewModel.TargetFor(newest.Id), Assert.Single(_notifications.Sent).Target);
        Assert.Contains("Newest", viewModel.LastResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_target_the_newest_real_email_row()
    {
        var connection = new FakeGraphConnection();
        var email = new FakeEmailService(connection, FakeEmailService.Item("m-1"), FakeEmailService.Item("m-2", minutesAgo: 5));
        var viewModel = new DevViewModel(_notifications, email: email);

        await viewModel.SendEmailDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(EmailViewModel.TargetFor("m-1"), Assert.Single(_notifications.Sent).Target);
    }

    [Fact]
    public async Task Should_target_the_first_real_pull_request_row()
    {
        var azureDevOps = new FakeAzureDevOpsService(new FakeAzureDevOpsConnection());
        azureDevOps.PullRequests.AddRange([FakeAzureDevOpsService.PullRequest(42), FakeAzureDevOpsService.PullRequest(7)]);
        var viewModel = new DevViewModel(_notifications, azureDevOps: azureDevOps);

        await viewModel.SendPullRequestDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(PullRequestsViewModel.TargetFor(42), Assert.Single(_notifications.Sent).Target);
    }

    [Fact]
    public async Task Should_target_the_first_real_work_item_row()
    {
        var azureDevOps = new FakeAzureDevOpsService(new FakeAzureDevOpsConnection());
        azureDevOps.WorkItems.AddRange([FakeAzureDevOpsService.WorkItem(1234), FakeAzureDevOpsService.WorkItem(99)]);
        var viewModel = new DevViewModel(_notifications, azureDevOps: azureDevOps);

        await viewModel.SendWorkItemDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(WorkItemsViewModel.TargetForWorkItem(1234), Assert.Single(_notifications.Sent).Target);
    }

    [Fact]
    public async Task Should_target_the_newest_real_mention_row_scanning_the_users_work_items()
    {
        var azureDevOps = new FakeAzureDevOpsService(new FakeAzureDevOpsConnection());
        azureDevOps.WorkItems.Add(FakeAzureDevOpsService.WorkItem(1));
        azureDevOps.Mentions.AddRange([FakeAzureDevOpsService.Mention(555), FakeAzureDevOpsService.Mention(444)]);
        var viewModel = new DevViewModel(_notifications, azureDevOps: azureDevOps);

        await viewModel.SendMentionDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(WorkItemsViewModel.TargetForMention(555), Assert.Single(_notifications.Sent).Target);
        Assert.Equal(1, Assert.Single(azureDevOps.MentionsRequestedFor!).Id);
    }

    // --- Deep-link test notifications: synthetic fallback ---------------------------

    [Fact]
    public async Task Should_fall_back_to_a_synthetic_target_on_the_same_page_when_no_service_is_registered()
    {
        var viewModel = new DevViewModel(_notifications);

        await viewModel.SendTodoDeepLinkCommand.ExecuteAsync(null);
        await viewModel.SendEmailDeepLinkCommand.ExecuteAsync(null);
        await viewModel.SendPullRequestDeepLinkCommand.ExecuteAsync(null);
        await viewModel.SendWorkItemDeepLinkCommand.ExecuteAsync(null);
        await viewModel.SendMentionDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(
            [
                TodoViewModel.TargetFor(Guid.Empty),
                EmailViewModel.TargetFor(DevViewModel.SyntheticEmailId),
                PullRequestsViewModel.TargetFor(0),
                WorkItemsViewModel.TargetForWorkItem(0),
                WorkItemsViewModel.TargetForMention(0),
            ],
            _notifications.Sent.Select(n => n.Target));
    }

    [Fact]
    public async Task Should_fall_back_to_a_synthetic_target_when_the_list_is_empty()
    {
        var viewModel = new DevViewModel(_notifications, todos: new FakeTodoRepository());

        await viewModel.SendTodoDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(TodoViewModel.TargetFor(Guid.Empty), Assert.Single(_notifications.Sent).Target);
        Assert.Contains("synthetic", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Should_fall_back_to_a_synthetic_target_and_report_why_when_loading_fails()
    {
        var email = new FakeEmailService(new FakeGraphConnection { IsSignedIn = false });
        var viewModel = new DevViewModel(_notifications, email: email);

        await viewModel.SendEmailDeepLinkCommand.ExecuteAsync(null);

        Assert.Equal(EmailViewModel.TargetFor(DevViewModel.SyntheticEmailId), Assert.Single(_notifications.Sent).Target);
        Assert.Contains("synthetic", viewModel.LastResult, StringComparison.OrdinalIgnoreCase);
    }

    // --- End to end through the real composition ------------------------------------

    [Fact]
    public async Task Should_land_on_and_highlight_the_todo_row_when_the_test_notification_is_activated()
    {
        var target = FakeTodoRepository.Item("Target");
        var services = new ServiceCollection();
        services.AddSingleton<ITodoRepository>(new FakeTodoRepository(target, FakeTodoRepository.Item("Other")));
        services.AddUi();
        services.AddSingleton<INotificationService>(_notifications);
        using var provider = services.BuildServiceProvider();
        ShellCompositionValidator.Validate(services, provider);

        var dev = provider.GetRequiredService<DevViewModel>();
        await dev.SendTodoDeepLinkCommand.ExecuteAsync(null);
        var sent = Assert.Single(_notifications.Sent);

        // What clicking the toast / bell entry does.
        var entry = provider.GetRequiredService<NotificationCenterViewModel>().Add(sent.Title, sent.Message, sent.Severity, sent.Target);
        entry.ActivateCommand.Execute(null);

        var page = Assert.IsType<TodoViewModel>(provider.GetRequiredService<INavigationService>().CurrentPage);
        await page.LoadCommand.ExecuteAsync(null); // what TodoView does when it appears
        Assert.Equal(target.Id, page.HighlightedItem?.Id);
    }

    [Fact]
    public void Should_contribute_the_last_sidebar_entry_and_a_view_in_the_default_composition()
    {
        using var provider = new ServiceCollection().AddUi().BuildServiceProvider();

        var menu = provider.GetRequiredService<IReadOnlyList<Core.Navigation.NavigationItem>>();
        Assert.Equal(typeof(DevViewModel), menu[^1].ViewModelType);
        Assert.Equal(typeof(DevView), provider.GetRequiredService<ViewRegistry>().GetViewType(typeof(DevViewModel)));
    }
}
