using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Presentation.Features.WorkItems;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.WorkItems;

public sealed class WorkItemsViewModelTests
{
    private readonly FakeAzureDevOpsConnection _connection = new();
    private readonly FakeAzureDevOpsService _azureDevOps;
    private readonly FakeSeenMentionRepository _seen = new();
    private readonly FakeBrowserLauncher _browser = new();

    public WorkItemsViewModelTests()
    {
        _azureDevOps = new FakeAzureDevOpsService(_connection);
        _azureDevOps.WorkItems.AddRange([FakeAzureDevOpsService.WorkItem(1), FakeAzureDevOpsService.WorkItem(2)]);
        _azureDevOps.Mentions.AddRange([FakeAzureDevOpsService.Mention(501, workItemId: 2), FakeAzureDevOpsService.Mention(101, workItemId: 1)]);
    }

    private WorkItemsViewModel Create() => new(_azureDevOps, _connection, _seen, _browser);

    private async Task<WorkItemsViewModel> LoadedAsync()
    {
        var viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // --- Loading ---------------------------------------------------------------------------

    [Fact]
    public async Task Should_show_the_work_items_and_the_mentions_on_them()
    {
        var viewModel = await LoadedAsync();

        Assert.Equal([1, 2], viewModel.Items.Select(r => r.Id));
        Assert.Equal([501, 101], viewModel.Mentions.Select(r => r.CommentId));
        Assert.Equal([1, 2], _azureDevOps.MentionsRequestedFor?.Select(w => w.Id));
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Should_format_the_rows_for_display()
    {
        var viewModel = await LoadedAsync();

        Assert.Equal("#1", viewModel.Items[0].IdText);
        Assert.Equal("P2", viewModel.Items[0].PriorityText);
        Assert.Equal("3 pts", viewModel.Items[0].EffortText);
        Assert.Equal("#2 Item 2", viewModel.Mentions[0].WorkItemText);
    }

    [Fact]
    public async Task Should_show_only_mentions_not_yet_opened_as_unread()
    {
        _seen.Seen.Add(101);

        var viewModel = await LoadedAsync();

        Assert.Equal([true, false], viewModel.Mentions.Select(r => r.IsUnread));
    }

    [Fact]
    public async Task Should_keep_the_work_items_and_hide_the_mentions_when_mentions_fail()
    {
        // Outer best-effort guard: the preview comments API (or identity lookup) failing wholesale must
        // only hide the Mentions section.
        _azureDevOps.MentionsFailure = new HttpRequestException("VS403357: preview API deactivated");

        var viewModel = await LoadedAsync();

        Assert.Equal([1, 2], viewModel.Items.Select(r => r.Id));
        Assert.Empty(viewModel.Mentions);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.NeedsToken);
    }

    [Fact]
    public async Task Should_hide_mentions_that_were_shown_before_when_a_refresh_cannot_load_them()
    {
        var viewModel = await LoadedAsync();
        viewModel.SelectedMention = viewModel.Mentions[0];
        _azureDevOps.MentionsFailure = new InvalidOperationException("boom");

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Mentions);
        Assert.Null(viewModel.SelectedMention);
        Assert.Equal(2, viewModel.Items.Count);
    }

    [Fact]
    public async Task Should_keep_the_work_items_and_hide_the_mentions_when_the_read_state_store_fails()
    {
        _seen.Failure = new InvalidOperationException("database locked");

        var viewModel = await LoadedAsync();

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Empty(viewModel.Mentions);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Should_show_every_mention_as_unread_when_no_read_state_store_is_registered()
    {
        var viewModel = new WorkItemsViewModel(_azureDevOps, _connection);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.All(viewModel.Mentions, r => Assert.True(r.IsUnread));
    }

    [Fact]
    public async Task Should_ask_for_a_token_when_none_is_stored()
    {
        _connection.HasToken = false;

        var viewModel = await LoadedAsync();

        Assert.True(viewModel.NeedsToken);
        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Should_show_an_error_when_the_work_items_fail_to_load()
    {
        _azureDevOps.Failure = new HttpRequestException("offline");

        var viewModel = await LoadedAsync();

        Assert.Contains("offline", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_degrade_gracefully_when_no_azure_devops_service_is_registered()
    {
        var viewModel = new WorkItemsViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsAvailable);
        Assert.Empty(viewModel.Items);
        Assert.Empty(viewModel.Mentions);
    }

    // --- Opening ----------------------------------------------------------------------------

    [Fact]
    public async Task Should_open_a_work_item_in_the_browser()
    {
        var viewModel = await LoadedAsync();

        viewModel.OpenWorkItemCommand.Execute(viewModel.Items[1]);

        Assert.Equal(["https://dev.azure.com/cegid/Alpha/_workitems/edit/2"], _browser.Opened);
    }

    [Fact]
    public async Task Should_open_the_commented_work_item_and_remember_the_mention_as_read()
    {
        var viewModel = await LoadedAsync();
        var mention = viewModel.Mentions[0];

        await viewModel.OpenMentionCommand.ExecuteAsync(mention);

        Assert.Equal(["https://dev.azure.com/cegid/Alpha/_workitems/edit/2"], _browser.Opened);
        Assert.False(mention.IsUnread);
        Assert.Contains(501, _seen.Seen);
    }

    [Fact]
    public async Task Should_not_store_the_read_state_again_for_a_mention_already_read()
    {
        _seen.Seen.Add(501);
        var viewModel = await LoadedAsync();

        await viewModel.OpenMentionCommand.ExecuteAsync(viewModel.Mentions[0]);

        Assert.Equal(0, _seen.MarkCalls);
        Assert.Single(_browser.Opened);
    }

    [Fact]
    public async Task Should_still_mark_the_mention_read_for_the_session_when_storing_that_fails()
    {
        var viewModel = await LoadedAsync();
        _seen.Failure = new InvalidOperationException("database locked");
        var mention = viewModel.Mentions[0];

        await viewModel.OpenMentionCommand.ExecuteAsync(mention);

        Assert.False(mention.IsUnread);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Single(_browser.Opened);
    }

    // --- Deep links: two element kinds on one page --------------------------------------------

    [Fact]
    public async Task Should_select_and_highlight_a_work_item_row_for_an_item_element_id()
    {
        var viewModel = await LoadedAsync();

        viewModel.FocusElement(WorkItemsViewModel.TargetForWorkItem(2).ElementId!);

        Assert.Equal(2, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
        Assert.Null(viewModel.HighlightedMention);
    }

    [Fact]
    public async Task Should_select_and_highlight_a_mention_row_for_a_mention_element_id()
    {
        var viewModel = await LoadedAsync();

        viewModel.FocusElement(WorkItemsViewModel.TargetForMention(101).ElementId!);

        Assert.Equal(101, viewModel.SelectedMention?.CommentId);
        Assert.Same(viewModel.SelectedMention, viewModel.HighlightedMention);
        Assert.Null(viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_not_confuse_a_mention_with_a_work_item_that_has_the_same_number()
    {
        _azureDevOps.WorkItems.Add(FakeAzureDevOpsService.WorkItem(101));
        var viewModel = await LoadedAsync();

        viewModel.FocusElement(WorkItemsViewModel.TargetForMention(101).ElementId!);

        Assert.Equal(101, viewModel.SelectedMention?.CommentId);
        Assert.Null(viewModel.SelectedItem);
    }

    [Fact]
    public async Task Should_apply_a_work_item_focus_request_made_before_loading_once_the_page_loads()
    {
        var viewModel = Create();

        viewModel.FocusElement(WorkItemsViewModel.TargetForWorkItem(1).ElementId!);
        Assert.Null(viewModel.HighlightedItem);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(1, viewModel.HighlightedItem?.Id);
        Assert.Same(viewModel.HighlightedItem, viewModel.SelectedItem);
    }

    [Fact]
    public async Task Should_apply_a_mention_focus_request_made_before_loading_once_the_mentions_load()
    {
        // Mentions load after (and depend on) the work items; the request must wait for both.
        var viewModel = Create();

        viewModel.FocusElement(WorkItemsViewModel.TargetForMention(501).ElementId!);
        Assert.Null(viewModel.HighlightedMention);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(501, viewModel.HighlightedMention?.CommentId);
        Assert.Same(viewModel.HighlightedMention, viewModel.SelectedMention);
    }

    [Fact]
    public async Task Should_keep_a_mention_focus_request_pending_until_a_token_is_entered()
    {
        _connection.HasToken = false;
        var viewModel = Create();
        viewModel.FocusElement(WorkItemsViewModel.TargetForMention(501).ElementId!);
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(viewModel.SelectedMention);

        viewModel.PersonalAccessToken = FakeAzureDevOpsConnection.VALID_TOKEN;
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(501, viewModel.SelectedMention?.CommentId);
        Assert.Same(viewModel.SelectedMention, viewModel.HighlightedMention);
    }

    [Theory]
    [InlineData("1")] // bare number: ambiguous, so not accepted
    [InlineData("item:99")] // no longer assigned
    [InlineData("mention:999")] // stale
    [InlineData("item:")]
    [InlineData("mention:abc")]
    [InlineData("Item:1")] // prefixes are case-sensitive
    [InlineData("comment:101")]
    public async Task Should_ignore_an_unknown_ambiguous_or_malformed_element_id(string elementId)
    {
        var viewModel = await LoadedAsync();

        viewModel.FocusElement(elementId);

        Assert.Null(viewModel.SelectedItem);
        Assert.Null(viewModel.HighlightedItem);
        Assert.Null(viewModel.SelectedMention);
        Assert.Null(viewModel.HighlightedMention);
    }

    [Fact]
    public async Task Should_not_reveal_a_mention_whose_section_could_not_be_loaded()
    {
        _azureDevOps.MentionsFailure = new HttpRequestException("404");
        var viewModel = Create();
        viewModel.FocusElement(WorkItemsViewModel.TargetForMention(501).ElementId!);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Null(viewModel.HighlightedMention);
        Assert.Equal(2, viewModel.Items.Count);
    }

    [Fact]
    public void Should_encode_the_two_element_kinds_with_distinct_prefixes()
    {
        var item = WorkItemsViewModel.TargetForWorkItem(1234);
        var mention = WorkItemsViewModel.TargetForMention(1234);

        Assert.Equal(typeof(WorkItemsViewModel), item.ViewModelType);
        Assert.Equal(typeof(WorkItemsViewModel), mention.ViewModelType);
        Assert.Equal("item:1234", item.ElementId);
        Assert.Equal("mention:1234", mention.ElementId);
    }
}
