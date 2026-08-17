using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class PullRequestsViewModelTests
{
    private static PullRequestItem Pr(int id, double ageDays) =>
        new(id, $"PR {id}", "Author", "Repo", DateTime.UtcNow.AddDays(-ageDays), "No vote", $"https://dev.azure.com/cegid/p/_git/r/pullrequest/{id}");

    private static FakeCredentialStore WithPat()
    {
        var store = new FakeCredentialStore();
        store.Save(CredentialKeys.AZURE_DEVOPS_PAT, "pat");
        return store;
    }

    [Fact]
    public async Task Refresh_without_a_pat_shows_the_configure_message()
    {
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), new FakeCredentialStore());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.NeedsConfiguration);
        Assert.Empty(vm.PullRequests);
    }

    [Fact]
    public async Task Refresh_with_pat_but_no_projects_prompts_to_add_a_project()
    {
        var settings = new FakeAzureDevOpsSettingsService(projects: []);
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), settings: settings);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.NeedsConfiguration);
        Assert.Contains("project", vm.ConfigurationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_loads_pull_requests_oldest_first()
    {
        var azdo = new FakeAzureDevOpsService(pullRequests: [Pr(1, 1), Pr(2, 8), Pr(3, 3)]);
        var vm = new PullRequestsViewModel(azdo, WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.NeedsConfiguration);
        Assert.Equal([8, 3, 1], vm.PullRequests.Select(r => r.AgeDays).ToArray());
    }

    [Fact]
    public async Task Refresh_with_an_empty_queue_sets_the_empty_flag()
    {
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.PullRequests);
    }

    [Fact]
    public async Task Refresh_on_service_failure_sets_an_error_message()
    {
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(throwOnCall: true), WithPat());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(vm.PullRequests);
    }

    [Fact]
    public void Open_launches_the_pull_request_url()
    {
        var launcher = new FakeBrowserLauncher();
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), launcher);
        var row = PullRequestRow.From(Pr(7, 0));

        vm.OpenCommand.Execute(row);

        Assert.Equal(row.Url, launcher.LastUrl);
    }

    [Fact]
    public async Task Review_notifies_prerequisite_error_when_git_missing()
    {
        var launcher = new FakeBrowserLauncher();
        var notification = new FakeNotificationService();
        var review = new FakePrReviewService(new PrerequisiteCheckResult(GitFound: false, ClaudeFound: true, WorkFolderIssue: null));
        var vm = new PullRequestsViewModel(
            new FakeAzureDevOpsService(), WithPat(), launcher,
            reviewService: review, notification: notification);
        var row = PullRequestRow.From(Pr(7, 0));

        await vm.ReviewPrCommand.ExecuteAsync(row);

        Assert.Equal(NotificationSeverity.WARNING, notification.LastSeverity);
        Assert.False(review.ReviewCalled);
        Assert.Null(launcher.LastUrl);
        Assert.False(row.IsReviewing);
        Assert.Contains("Blocked", row.ReviewLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_opens_report_when_review_succeeds()
    {
        var launcher = new FakeBrowserLauncher();
        var notification = new FakeNotificationService();
        var review = new FakePrReviewService(htmlPath: @"C:\reviews\Repo-PR7.html");
        var vm = new PullRequestsViewModel(
            new FakeAzureDevOpsService(), WithPat(), launcher,
            reviewService: review, notification: notification);
        var row = PullRequestRow.From(Pr(7, 0));

        await vm.ReviewPrCommand.ExecuteAsync(row);

        Assert.True(review.ReviewCalled);
        Assert.Equal(@"C:\reviews\Repo-PR7.html", launcher.LastUrl);
        Assert.Equal(NotificationSeverity.SUCCESS, notification.LastSeverity);
        Assert.False(row.IsReviewing);
        Assert.Contains("Done", row.ReviewLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_passes_no_agent_override_by_default()
    {
        var review = new FakePrReviewService();
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), reviewService: review);
        var row = PullRequestRow.From(Pr(7, 0));

        await vm.ReviewPrCommand.ExecuteAsync(row);

        Assert.Null(review.LastAgentFilePath);
        Assert.Equal("Default", row.AgentLabel);
    }

    [Fact]
    public async Task Review_passes_the_row_agent_override_to_the_review_service()
    {
        var review = new FakePrReviewService();
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), reviewService: review);
        var row = PullRequestRow.From(Pr(7, 0));
        row.AgentOverridePath = @"C:\agents\security-review.md";

        await vm.ReviewPrCommand.ExecuteAsync(row);

        Assert.Equal(@"C:\agents\security-review.md", review.LastAgentFilePath);
        Assert.Equal("security-review", row.AgentLabel);
    }

    [Fact]
    public async Task ChooseReviewAgent_sets_the_row_override_when_a_file_is_picked()
    {
        var picker = new FakeFilePicker(@"C:\agents\security-review.md");
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), filePicker: picker);
        var row = PullRequestRow.From(Pr(7, 0));

        await vm.ChooseReviewAgentCommand.ExecuteAsync(row);

        Assert.Equal(@"C:\agents\security-review.md", row.AgentOverridePath);
        Assert.True(row.HasAgentOverride);
    }

    [Fact]
    public async Task ChooseReviewAgent_leaves_the_row_unchanged_when_the_picker_is_cancelled()
    {
        var picker = new FakeFilePicker(result: null);
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat(), filePicker: picker);
        var row = PullRequestRow.From(Pr(7, 0));

        await vm.ChooseReviewAgentCommand.ExecuteAsync(row);

        Assert.Null(row.AgentOverridePath);
        Assert.False(row.HasAgentOverride);
    }

    [Fact]
    public void ClearReviewAgentOverride_resets_the_row_to_the_default()
    {
        var vm = new PullRequestsViewModel(new FakeAzureDevOpsService(), WithPat());
        var row = PullRequestRow.From(Pr(7, 0));
        row.AgentOverridePath = @"C:\agents\security-review.md";

        vm.ClearReviewAgentOverrideCommand.Execute(row);

        Assert.Null(row.AgentOverridePath);
        Assert.Equal("Default", row.AgentLabel);
    }

    [Fact]
    public async Task Cancel_review_requests_cancellation_of_the_running_review()
    {
        var launcher = new FakeBrowserLauncher();
        var notification = new FakeNotificationService();
        var center = new NotificationCenterViewModel();
        var review = new CancellingPrReviewService();
        var vm = new PullRequestsViewModel(
            new FakeAzureDevOpsService(), WithPat(), launcher,
            reviewService: review, notification: notification, notificationCenter: center);
        var row = PullRequestRow.From(Pr(7, 0));

        // The service blocks until cancelled; CancelReview releases it.
        var reviewTask = vm.ReviewPrCommand.ExecuteAsync(row);
        await review.Started;
        Assert.True(center.HasActive); // bell shows the review in progress
        vm.CancelReviewCommand.Execute(row);
        await reviewTask;

        Assert.Equal(NotificationSeverity.WARNING, notification.LastSeverity);
        Assert.False(row.IsReviewing);
        Assert.False(center.HasActive); // cleared once finished
        Assert.Contains("Cancelled", row.ReviewLogText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, true, false)]   // > 2 days -> aging
    [InlineData(5, true, false)]
    [InlineData(6, false, true)]   // > 5 days -> stale
    public void Row_age_highlight_thresholds(double ageDays, bool aging, bool stale)
    {
        var row = PullRequestRow.From(Pr(1, ageDays));

        Assert.Equal(aging, row.IsAging);
        Assert.Equal(stale, row.IsStale);
    }

    [Theory]
    [InlineData("Approved", "success")]
    [InlineData("Approved with suggestions", "successAlt")]
    [InlineData("Waiting for author", "warning")]
    [InlineData("Rejected", "danger")]
    [InlineData("No vote", "neutral")]
    [InlineData("anything else", "neutral")]
    public void Row_maps_vote_status_to_badge_category(string voteStatus, string expected)
    {
        var item = new PullRequestItem(1, "PR", "Author", "Repo", DateTime.UtcNow, voteStatus, "https://example/1");
        var row = PullRequestRow.From(item);

        Assert.Equal(expected, row.BadgeCategory);
    }
}
