using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Presentation.Features.PullRequests;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.PullRequests;

public sealed class PullRequestsViewModelTests
{
    private readonly FakeAzureDevOpsConnection _connection = new();
    private readonly FakeAzureDevOpsService _azureDevOps;
    private readonly FakeBrowserLauncher _browser = new();

    public PullRequestsViewModelTests()
    {
        _azureDevOps = new FakeAzureDevOpsService(_connection);
    }

    private PullRequestsViewModel Create() => new(_azureDevOps, _connection, _browser);

    private async Task<PullRequestsViewModel> LoadedAsync(params int[] ids)
    {
        _azureDevOps.PullRequests.AddRange(ids.Select(id => FakeAzureDevOpsService.PullRequest(id)));
        var viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // --- Loading ---------------------------------------------------------------------------

    [Fact]
    public async Task Should_show_the_pull_requests_when_loaded()
    {
        var viewModel = await LoadedAsync(3, 1);

        Assert.Equal([3, 1], viewModel.Items.Select(r => r.Id));
        Assert.False(viewModel.NeedsToken);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Should_describe_role_and_vote_for_display()
    {
        _azureDevOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(1) with { MyVote = PullRequestVote.WAITING });
        _azureDevOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(2, PullRequestRole.AUTHOR));
        var viewModel = Create();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(["Reviewing", "Authored"], viewModel.Items.Select(r => r.RoleText));
        Assert.Equal(["Waiting for author", ""], viewModel.Items.Select(r => r.VoteText));
        Assert.Equal("!1", viewModel.Items[0].IdText);
        Assert.Equal("feature/x → main", viewModel.Items[0].BranchesText);
    }

    [Fact]
    public async Task Should_ask_for_a_token_instead_of_showing_an_error_when_none_is_stored()
    {
        _connection.HasToken = false;

        var viewModel = await LoadedAsync(1);

        Assert.True(viewModel.NeedsToken);
        Assert.NotNull(viewModel.ConnectionProblem);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task Should_load_and_forget_the_typed_token_after_connecting()
    {
        _connection.HasToken = false;
        var viewModel = await LoadedAsync(1);
        Assert.False(viewModel.ConnectCommand.CanExecute(null)); // nothing typed yet

        viewModel.PersonalAccessToken = FakeAzureDevOpsConnection.VALID_TOKEN;
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal([FakeAzureDevOpsConnection.VALID_TOKEN], _connection.ConnectAttempts);
        Assert.False(viewModel.NeedsToken);
        Assert.Equal("", viewModel.PersonalAccessToken);
        Assert.Equal([1], viewModel.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Should_keep_the_prompt_and_show_why_when_the_token_is_rejected()
    {
        _connection.HasToken = false;
        var viewModel = await LoadedAsync(1);

        viewModel.PersonalAccessToken = "typo";
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsToken);
        Assert.Contains("rejected", viewModel.ConnectionProblem, StringComparison.Ordinal);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task Should_show_an_error_when_loading_fails()
    {
        _azureDevOps.Failure = new HttpRequestException("offline");

        var viewModel = await LoadedAsync();

        Assert.Contains("offline", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.NeedsToken);
    }

    [Fact]
    public async Task Should_degrade_gracefully_when_no_azure_devops_service_is_registered()
    {
        var viewModel = new PullRequestsViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsAvailable);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_keep_the_selection_across_a_refresh_and_pick_up_new_pull_requests()
    {
        var viewModel = await LoadedAsync(1, 2);
        viewModel.SelectedItem = viewModel.Items[1];
        _azureDevOps.PullRequests.Insert(0, FakeAzureDevOpsService.PullRequest(9));

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal([9, 1, 2], viewModel.Items.Select(r => r.Id));
        Assert.Equal(2, viewModel.SelectedItem?.Id);
    }

    [Fact]
    public async Task Should_open_the_pull_request_in_the_browser()
    {
        var viewModel = await LoadedAsync(42);

        viewModel.OpenCommand.Execute(viewModel.Items[0]);

        Assert.Equal(["https://dev.azure.com/cegid/Alpha/_git/web/pullrequest/42"], _browser.Opened);
    }

    // --- Deep links (IDeepLinkTarget) ---------------------------------------------------------

    [Fact]
    public async Task Should_select_and_highlight_the_row_when_focusing_a_loaded_pull_request()
    {
        var viewModel = await LoadedAsync(1, 42);

        viewModel.FocusElement("42");

        Assert.Equal(42, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_apply_a_focus_request_made_before_loading_once_the_list_loads()
    {
        _azureDevOps.PullRequests.AddRange([FakeAzureDevOpsService.PullRequest(1), FakeAzureDevOpsService.PullRequest(42)]);
        var viewModel = Create();

        viewModel.FocusElement("42");
        Assert.Null(viewModel.SelectedItem);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(42, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_keep_a_focus_request_pending_until_a_token_is_entered()
    {
        // A PR notification clicked while no token is stored: the row is revealed once the user connects.
        _connection.HasToken = false;
        _azureDevOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(42));
        var viewModel = Create();
        viewModel.FocusElement("42");
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(viewModel.SelectedItem);

        viewModel.PersonalAccessToken = FakeAzureDevOpsConnection.VALID_TOKEN;
        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(42, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Theory]
    [InlineData("7")] // completed or abandoned since the notification
    [InlineData("forty-two")]
    [InlineData("-42")]
    [InlineData(" 42")]
    public async Task Should_ignore_a_focus_request_for_an_unknown_or_malformed_id(string elementId)
    {
        var viewModel = await LoadedAsync(42);

        viewModel.FocusElement(elementId);

        Assert.Null(viewModel.SelectedItem);
        Assert.Null(viewModel.HighlightedItem);
    }

    [Fact]
    public void Should_build_a_navigation_target_whose_element_id_is_the_pull_request_id()
    {
        var target = PullRequestsViewModel.TargetFor(1234);

        Assert.Equal(typeof(PullRequestsViewModel), target.ViewModelType);
        Assert.Equal("1234", target.ElementId);
    }

    // --- Cancellation ----------------------------------------------------------------------

    [Fact]
    public async Task Should_not_fault_when_a_running_load_is_superseded_by_a_new_one()
    {
        _azureDevOps.PullRequests.Add(FakeAzureDevOpsService.PullRequest(1));
        _azureDevOps.PullRequestsGate = new TaskCompletionSource();
        var viewModel = Create();

        var first = viewModel.RefreshCommand.ExecuteAsync(null);
        _azureDevOps.PullRequestsGate = null;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        await first;

        Assert.Equal([1], viewModel.Items.Select(r => r.Id));
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Should_show_an_error_when_a_library_cancels_on_its_own()
    {
        _azureDevOps.Failure = new TaskCanceledException("timed out");
        var viewModel = Create();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("timed out", viewModel.ErrorMessage, StringComparison.Ordinal);
    }
}
