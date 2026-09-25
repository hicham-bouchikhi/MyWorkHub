using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Features.PullRequests;
using MyWorkHub.UI.Features.WorkItems;

namespace MyWorkHub.UI.Tests.Features.AzureDevOps;

/// <summary>
/// End-to-end: a notification carrying a <see cref="NavigationTarget"/> → its bell-history entry is
/// activated (exactly what clicking the toast or the entry does) → NavigationService → the page's
/// FocusElement → that specific pull request / mention / work item row is selected and highlighted.
/// Runs on the real composition (modules discovered, real NavigationService and NotificationCenterViewModel).
/// </summary>
public sealed class AzureDevOpsDeepLinkTests
{
    private static ServiceProvider Compose()
    {
        var connection = new FakeAzureDevOpsConnection();
        var azureDevOps = new FakeAzureDevOpsService(connection);
        azureDevOps.PullRequests.AddRange([FakeAzureDevOpsService.PullRequest(1), FakeAzureDevOpsService.PullRequest(42)]);
        azureDevOps.WorkItems.AddRange([FakeAzureDevOpsService.WorkItem(7), FakeAzureDevOpsService.WorkItem(8)]);
        azureDevOps.Mentions.AddRange([FakeAzureDevOpsService.Mention(900, workItemId: 7), FakeAzureDevOpsService.Mention(901, workItemId: 8)]);

        var services = new ServiceCollection();
        services.AddSingleton<IAzureDevOpsConnectionService>(connection);
        services.AddSingleton<IAzureDevOpsService>(azureDevOps);
        services.AddSingleton<ISeenMentionRepository>(new FakeSeenMentionRepository());
        services.AddUi();
        return services.BuildServiceProvider();
    }

    /// <summary>Raises a notification into the shared history and clicks it.</summary>
    private static void ClickNotification(IServiceProvider provider, NavigationTarget target)
        => provider.GetRequiredService<NotificationCenterViewModel>()
            .Add("Azure DevOps", "Something needs you", NotificationSeverity.INFORMATION, target)
            .ActivateCommand.Execute(null);

    [Fact]
    public async Task Should_land_on_the_pull_request_row_when_its_notification_is_clicked_before_first_visit()
    {
        using var provider = Compose();
        var navigation = provider.GetRequiredService<INavigationService>();

        ClickNotification(provider, PullRequestsViewModel.TargetFor(42));
        var page = Assert.IsType<PullRequestsViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null); // what PullRequestsView does when it appears

        Assert.Equal(42, page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }

    [Fact]
    public async Task Should_land_on_the_mention_row_when_its_notification_is_clicked_before_first_visit()
    {
        using var provider = Compose();
        var navigation = provider.GetRequiredService<INavigationService>();

        ClickNotification(provider, WorkItemsViewModel.TargetForMention(901));
        var page = Assert.IsType<WorkItemsViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null); // what WorkItemsView does when it appears

        Assert.Equal(901, page.SelectedMention?.CommentId);
        Assert.Same(page.SelectedMention, page.HighlightedMention);
        Assert.Null(page.HighlightedItem);
    }

    [Fact]
    public async Task Should_retarget_the_cached_work_items_page_from_a_mention_to_a_work_item()
    {
        using var provider = Compose();
        var navigation = provider.GetRequiredService<INavigationService>();
        ClickNotification(provider, WorkItemsViewModel.TargetForMention(900));
        var page = Assert.IsType<WorkItemsViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null);
        page.HighlightedMention = null; // the view's ScrollIntoViewBehavior consumes the request

        navigation.NavigateTo(new NavigationTarget(typeof(PullRequestsViewModel)));
        ClickNotification(provider, WorkItemsViewModel.TargetForWorkItem(8));

        Assert.Same(page, navigation.CurrentPage);
        Assert.Equal(8, page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
        Assert.Null(page.HighlightedMention);
    }

    [Fact]
    public async Task Should_focus_the_pull_request_on_the_cached_page_when_navigating_back_with_a_target()
    {
        using var provider = Compose();
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.NavigateTo(new NavigationTarget(typeof(PullRequestsViewModel)));
        var page = Assert.IsType<PullRequestsViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.Null(page.SelectedItem);

        ClickNotification(provider, PullRequestsViewModel.TargetFor(1));

        Assert.Same(page, navigation.CurrentPage);
        Assert.Equal(1, page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }

    [Theory]
    [InlineData(typeof(PullRequestsViewModel), typeof(PullRequestsView))]
    [InlineData(typeof(WorkItemsViewModel), typeof(WorkItemsView))]
    public void Should_contribute_a_sidebar_entry_and_a_view_in_the_default_composition(Type viewModelType, Type viewType)
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetRequiredService<IReadOnlyList<NavigationItem>>(), i => i.ViewModelType == viewModelType);
        Assert.Equal(viewType, provider.GetRequiredService<ViewRegistry>().GetViewType(viewModelType));
        ShellCompositionValidator.Validate(services, provider);
    }

    [Fact]
    public void Should_list_the_azure_devops_pages_after_the_microsoft_365_pages_and_before_todo()
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        var labels = provider.GetRequiredService<IReadOnlyList<NavigationItem>>().Select(i => i.Label).ToList();

        Assert.Equal(["Teams", "Pull requests", "Work items", "Todo"], labels.SkipWhile(l => l != "Teams"));
    }
}
