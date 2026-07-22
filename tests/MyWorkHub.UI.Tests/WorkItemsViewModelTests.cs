using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class WorkItemsViewModelTests
{
    private static WorkItem Wi(int id, string state, DateTime? due = null, string effort = "") =>
        new(id, $"Item {id}", "User Story", state, "2", due, $"https://dev.azure.com/cegid/p/_workitems/edit/{id}", effort);

    private static WorkItemMention Mention(int workItemId, int commentId) =>
        new(workItemId, $"Item {workItemId}", $"https://dev.azure.com/cegid/p/_workitems/edit/{workItemId}",
            commentId, "Alice", DateTime.UtcNow.AddMinutes(-5), "Hey @Hicham BOUCHIKHI please check this.");

    private static FakeCredentialStore WithPat()
    {
        var store = new FakeCredentialStore();
        store.Save(CredentialKeys.AZURE_DEVOPS_PAT, "pat");
        return store;
    }

    [Fact]
    public async Task Refresh_without_a_pat_shows_the_configure_message()
    {
        var vm = new WorkItemsViewModel(new FakeAzureDevOpsService(), new FakeCredentialStore());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.NeedsConfiguration);
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Refresh_with_pat_but_no_projects_prompts_to_add_a_project()
    {
        var settings = new FakeAzureDevOpsSettingsService(projects: []);
        var vm = new WorkItemsViewModel(new FakeAzureDevOpsService(), WithPat(), settings: settings);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.NeedsConfiguration);
        Assert.Contains("project", vm.ConfigurationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_groups_by_state_in_canonical_order()
    {
        var azdo = new FakeAzureDevOpsService(workItems:
        [
            Wi(1, "Closed"),
            Wi(2, "New"),
            Wi(3, "Active"),
            Wi(4, "Resolved"),
        ]);
        var vm = new WorkItemsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["Active", "New", "Resolved", "Closed"], vm.Groups.Select(g => g.Name).ToArray());
    }

    [Fact]
    public async Task Refresh_surfaces_effort_on_rows()
    {
        var azdo = new FakeAzureDevOpsService(workItems: [Wi(1, "Active", effort: "8")]);
        var vm = new WorkItemsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = vm.Groups.Single().Items.Single();
        Assert.Equal("8", row.Effort);
    }

    [Theory]
    [InlineData("Active", "info")]
    [InlineData("Committed", "info")]
    [InlineData("In Progress", "info")]
    [InlineData("Doing", "info")]
    [InlineData("New", "neutral")]
    [InlineData("To Do", "neutral")]
    [InlineData("Proposed", "neutral")]
    [InlineData("Resolved", "success")]
    [InlineData("Closed", "success")]
    [InlineData("Done", "success")]
    [InlineData("Completed", "success")]
    [InlineData("Removed", "danger")]
    [InlineData("Whatever", "neutral")]
    public void Row_maps_state_to_badge_category(string state, string expected)
    {
        var row = WorkItemRow.From(Wi(1, state));

        Assert.Equal(expected, row.BadgeCategory);
    }

    [Fact]
    public void Row_is_overdue_when_due_date_passed_and_not_done()
    {
        var overdue = WorkItemRow.From(Wi(1, "Active", DateTime.Today.AddDays(-1)));
        var doneButPast = WorkItemRow.From(Wi(2, "Closed", DateTime.Today.AddDays(-1)));
        var future = WorkItemRow.From(Wi(3, "Active", DateTime.Today.AddDays(3)));

        Assert.True(overdue.IsOverdue);
        Assert.False(doneButPast.IsOverdue);
        Assert.False(future.IsOverdue);
    }

    [Fact]
    public async Task Open_launches_the_work_item_url()
    {
        var launcher = new FakeBrowserLauncher();
        var azdo = new FakeAzureDevOpsService(workItems: [Wi(42, "Active")]);
        var vm = new WorkItemsViewModel(azdo, WithPat(), launcher);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Groups.Single().Items.Single();

        vm.OpenCommand.Execute(row);

        Assert.Equal(row.Url, launcher.LastUrl);
    }

    [Fact]
    public async Task Refresh_on_service_failure_sets_an_error_message()
    {
        var vm = new WorkItemsViewModel(new FakeAzureDevOpsService(throwOnCall: true), WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(vm.Groups);
    }

    [Fact]
    public async Task Refresh_populates_mentions_when_service_returns_them()
    {
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            mentions: [Mention(1, 100)]);
        var vm = new WorkItemsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasMentions);
        Assert.Single(vm.Mentions);
        Assert.Equal(100, vm.Mentions[0].CommentId);
    }

    [Fact]
    public async Task Refresh_mentions_hidden_when_no_mentions_returned()
    {
        var azdo = new FakeAzureDevOpsService(workItems: [Wi(1, "Active")], mentions: []);
        var vm = new WorkItemsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasMentions);
        Assert.Empty(vm.Mentions);
    }

    [Fact]
    public async Task Refresh_new_mentions_show_unseen_state()
    {
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            mentions: [Mention(1, 100)]);
        var seenRepo = new FakeSeenMentionRepository(); // nothing seen yet
        var vm = new WorkItemsViewModel(azdo, WithPat(), seenMentions: seenRepo);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.Mentions[0].IsNew);
        Assert.True(vm.HasNewMentions);
    }

    [Fact]
    public async Task Refresh_already_seen_mention_shows_as_not_new()
    {
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            mentions: [Mention(1, 100)]);
        var seenRepo = new FakeSeenMentionRepository(initialSeenIds: [100]);
        var vm = new WorkItemsViewModel(azdo, WithPat(), seenMentions: seenRepo);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.Mentions[0].IsNew);
        Assert.False(vm.HasNewMentions);
    }

    [Fact]
    public async Task OpenMention_opens_url_and_calls_mark_seen()
    {
        var launcher = new FakeBrowserLauncher();
        var seenRepo = new FakeSeenMentionRepository();
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            mentions: [Mention(1, 100)]);
        var vm = new WorkItemsViewModel(azdo, WithPat(), launcher, seenMentions: seenRepo);
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Mentions[0];

        await vm.OpenMentionCommand.ExecuteAsync(row);

        Assert.Equal(row.Url, launcher.LastUrl);
        Assert.Equal(1, seenRepo.MarkSeenCallCount);
        Assert.Equal(100, seenRepo.LastMarkedSeenId);
    }

    [Fact]
    public async Task OpenMention_marks_row_as_not_new()
    {
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            mentions: [Mention(1, 100)]);
        var vm = new WorkItemsViewModel(azdo, WithPat(), seenMentions: new FakeSeenMentionRepository());
        await vm.RefreshCommand.ExecuteAsync(null);
        var row = vm.Mentions[0];
        Assert.True(row.IsNew);

        await vm.OpenMentionCommand.ExecuteAsync(row);

        Assert.False(row.IsNew);
    }

    [Fact]
    public async Task Refresh_does_not_set_error_when_only_mention_fetch_fails()
    {
        var azdo = new FakeAzureDevOpsService(
            workItems: [Wi(1, "Active")],
            throwOnMentions: true);
        var vm = new WorkItemsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        // Work items loaded fine
        Assert.NotNull(vm.Groups);
        Assert.Single(vm.Groups);
        // No error surfaced to the user
        Assert.Null(vm.ErrorMessage);
        // Mentions section stays empty
        Assert.False(vm.HasMentions);
    }
}
