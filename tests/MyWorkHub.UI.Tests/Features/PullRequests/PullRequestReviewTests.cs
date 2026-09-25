using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.PrReview;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.PullRequests;

public sealed class PullRequestReviewTests
{
    private readonly FakeAzureDevOpsConnection _connection = new();
    private readonly FakeAzureDevOpsService _azureDevOps;
    private readonly FakePrReviewService _review = new();
    private readonly FakeFilePicker _picker = new();
    private readonly RecordingNotificationService _notifications = new();
    private readonly NotificationCenterViewModel _center = new(new FakeNavigationService());

    public PullRequestReviewTests()
    {
        _azureDevOps = new FakeAzureDevOpsService(_connection);
        _azureDevOps.PullRequests.AddRange([FakeAzureDevOpsService.PullRequest(42), FakeAzureDevOpsService.PullRequest(7)]);
    }

    private async Task<PullRequestsViewModel> LoadedAsync(IPrReviewService? review = null, IFilePicker? picker = null)
    {
        var viewModel = new PullRequestsViewModel(_azureDevOps, _connection, new FakeBrowserLauncher(), review, picker, _notifications, _center);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Should_review_the_row_and_show_the_report()
    {
        var viewModel = await LoadedAsync(_review, _picker);

        await viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);

        var (pr, agent) = Assert.Single(_review.Requests);
        Assert.Equal(42, pr.Id);
        Assert.Null(agent);
        Assert.Same(viewModel.Items[0], viewModel.ReviewedItem);
        Assert.Equal("Report for !42", viewModel.ReviewOutput);
        Assert.Null(viewModel.ReviewError);
        Assert.False(viewModel.IsReviewing);
    }

    [Fact]
    public async Task Should_notify_with_a_link_back_to_the_reviewed_row()
    {
        var viewModel = await LoadedAsync(_review);

        await viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);

        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(("Review ready", NotificationSeverity.SUCCESS), (notification.Title, notification.Severity));
        Assert.Equal(PullRequestsViewModel.TargetFor(42), notification.Target);
    }

    [Fact]
    public async Task Should_show_the_review_in_the_bell_and_block_other_reviews_while_running()
    {
        _review.Gate = new TaskCompletionSource();
        var viewModel = await LoadedAsync(_review, _picker);

        var running = viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.True(viewModel.IsReviewing);
        Assert.Equal("Reviewing !42", Assert.Single(_center.ActiveOperations).Label);
        Assert.False(viewModel.ReviewCommand.CanExecute(viewModel.Items[1]));
        Assert.False(viewModel.ReviewWithAgentCommand.CanExecute(viewModel.Items[1]));
        Assert.True(viewModel.CancelReviewCommand.CanExecute(null));

        _review.Gate.SetResult();
        await running;

        Assert.Empty(_center.ActiveOperations);
        Assert.True(viewModel.ReviewCommand.CanExecute(viewModel.Items[1]));
        Assert.False(viewModel.CancelReviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_stop_the_review_from_the_page()
    {
        _review.Gate = new TaskCompletionSource();
        var viewModel = await LoadedAsync(_review);

        var running = viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);
        viewModel.CancelReviewCommand.Execute(null);
        await running;

        Assert.Equal("Review cancelled.", viewModel.ReviewError);
        Assert.Empty(_center.ActiveOperations);
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task Should_stop_the_review_from_the_bell()
    {
        _review.Gate = new TaskCompletionSource();
        var viewModel = await LoadedAsync(_review);

        var running = viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);
        _center.ActiveOperations[0].CancelCommand.Execute(null);
        await running;

        Assert.Equal("Review cancelled.", viewModel.ReviewError);
        Assert.False(viewModel.IsReviewing);
    }

    [Fact]
    public async Task Should_show_and_notify_why_a_review_failed()
    {
        _review.Failure = new InvalidOperationException("git clone failed (exit code 128)");
        var viewModel = await LoadedAsync(_review);

        await viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Equal("Review failed: git clone failed (exit code 128)", viewModel.ReviewError);
        Assert.Equal(NotificationSeverity.ERROR, Assert.Single(_notifications.Sent).Severity);
        Assert.False(viewModel.IsReviewing);
    }

    [Fact]
    public async Task Should_review_with_a_picked_agent_file_for_that_review_only()
    {
        _picker.Result = "/home/me/security-review.md";
        var viewModel = await LoadedAsync(_review, _picker);

        await viewModel.ReviewWithAgentCommand.ExecuteAsync(viewModel.Items[1]);
        await viewModel.ReviewCommand.ExecuteAsync(viewModel.Items[1]);

        Assert.Equal([(7, "/home/me/security-review.md"), (7, null)], _review.Requests.Select(r => (r.Pr.Id, r.AgentFilePath)));
        Assert.Equal([".md"], _picker.LastExtensions);
    }

    [Fact]
    public async Task Should_not_review_when_the_agent_file_choice_is_cancelled()
    {
        _picker.Result = null;
        var viewModel = await LoadedAsync(_review, _picker);

        await viewModel.ReviewWithAgentCommand.ExecuteAsync(viewModel.Items[0]);

        Assert.Empty(_review.Requests);
        Assert.Null(viewModel.ReviewedItem);
    }

    [Fact]
    public async Task Should_hide_review_when_no_review_service_is_registered()
    {
        var viewModel = await LoadedAsync(review: null, _picker);

        Assert.False(viewModel.IsReviewAvailable);
        Assert.False(viewModel.CanPickReviewAgent);
        Assert.False(viewModel.ReviewCommand.CanExecute(viewModel.Items[0]));
    }

    [Fact]
    public async Task Should_hide_the_agent_file_choice_when_no_file_picker_is_registered()
    {
        var viewModel = await LoadedAsync(_review, picker: null);

        Assert.True(viewModel.IsReviewAvailable);
        Assert.False(viewModel.CanPickReviewAgent);
        Assert.False(viewModel.ReviewWithAgentCommand.CanExecute(viewModel.Items[0]));
    }

    private sealed class FakePrReviewService : IPrReviewService
    {
        public List<(PullRequestItem Pr, string? AgentFilePath)> Requests { get; } = [];

        public Exception? Failure { get; set; }

        /// <summary>When set, the review waits for this gate (or cancellation).</summary>
        public TaskCompletionSource? Gate { get; set; }

        public async Task<string> ReviewAsync(
            PullRequestItem pr, IProgress<string>? progress = null, string? agentFilePath = null, CancellationToken ct = default)
        {
            Requests.Add((pr, agentFilePath));
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(ct);
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            return "Report for !" + pr.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private sealed class FakeFilePicker : IFilePicker
    {
        public string? Result { get; set; }

        public IReadOnlyList<string>? LastExtensions { get; private set; }

        public Task<string?> PickFileAsync(string title, string? startPath = null, IReadOnlyList<string>? extensions = null)
        {
            LastExtensions = extensions;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<(string Title, string Message, NotificationSeverity Severity, NavigationTarget? Target)> Sent { get; } = [];

        public void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.INFORMATION, NavigationTarget? target = null)
            => Sent.Add((title, message, severity, target));
    }
}
