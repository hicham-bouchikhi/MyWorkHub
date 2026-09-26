using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.EmailSummary;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.UI.Tests.Features.GraphAuth;

namespace MyWorkHub.UI.Tests.Features.Email;

public sealed class EmailSummaryTests
{
    private readonly FakeGraphConnection _connection = new();
    private readonly FakeEmailSummaryService _summary = new();

    private async Task<EmailViewModel> LoadedAsync(params string[] ids)
    {
        var email = new FakeEmailService(_connection, [.. ids.Select(id => FakeEmailService.Item(id))]);
        var viewModel = new EmailViewModel(email, _connection, _summary);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Should_summarize_the_loaded_emails_and_show_the_digest()
    {
        var viewModel = await LoadedAsync("a", "b");

        await viewModel.SummarizeCommand.ExecuteAsync(null);

        Assert.Equal(["a", "b"], Assert.Single(_summary.Requests).Select(e => e.Id));
        Assert.Equal("Digest of 2", viewModel.SummaryText);
        Assert.Null(viewModel.SummaryStatus);
        Assert.Null(viewModel.SummaryError);
    }

    [Fact]
    public async Task Should_only_allow_summarizing_once_emails_are_loaded()
    {
        var email = new FakeEmailService(_connection, FakeEmailService.Item("a"));
        var viewModel = new EmailViewModel(email, _connection, _summary);
        Assert.True(viewModel.IsSummaryAvailable);
        Assert.False(viewModel.SummarizeCommand.CanExecute(null));

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.SummarizeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_not_allow_summarizing_an_empty_mailbox()
    {
        var viewModel = await LoadedAsync();

        Assert.False(viewModel.SummarizeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_show_why_the_digest_failed()
    {
        _summary.Failure = new CliAgentException("Could not start the Claude CLI ('claude').");
        var viewModel = await LoadedAsync("a");

        await viewModel.SummarizeCommand.ExecuteAsync(null);

        Assert.Equal("Could not summarize: Could not start the Claude CLI ('claude').", viewModel.SummaryError);
        Assert.Null(viewModel.SummaryText);
        Assert.Null(viewModel.SummaryStatus);
    }

    [Fact]
    public async Task Should_offer_sign_in_when_the_digest_finds_the_session_expired()
    {
        _summary.Failure = new GraphNotConnectedException();
        var viewModel = await LoadedAsync("a");

        await viewModel.SummarizeCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsSignIn);
        Assert.Null(viewModel.SummaryError);
    }

    [Fact]
    public async Task Should_stop_a_running_digest_on_request()
    {
        _summary.Gate = new TaskCompletionSource();
        var viewModel = await LoadedAsync("a");

        var running = viewModel.SummarizeCommand.ExecuteAsync(null);
        Assert.True(viewModel.SummarizeCommand.IsRunning);
        viewModel.SummarizeCancelCommand.Execute(null);
        await running;

        Assert.Equal("Summary cancelled.", viewModel.SummaryStatus);
        Assert.Null(viewModel.SummaryText);
    }

    [Fact]
    public async Task Should_hide_the_digest_when_no_summary_service_is_registered()
    {
        var email = new FakeEmailService(_connection, FakeEmailService.Item("a"));
        var viewModel = new EmailViewModel(email, _connection);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsSummaryAvailable);
        Assert.False(viewModel.SummarizeCommand.CanExecute(null));
    }

    private sealed class FakeEmailSummaryService : IEmailSummaryService
    {
        public List<IReadOnlyList<EmailItem>> Requests { get; } = [];

        public Exception? Failure { get; set; }

        /// <summary>When set, the digest waits for this gate (or cancellation).</summary>
        public TaskCompletionSource? Gate { get; set; }

        public async Task<string> SummarizeAsync(IReadOnlyList<EmailItem> emails, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            Requests.Add(emails);
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(ct);
            }

            if (Failure is not null)
            {
                throw Failure;
            }

            return "Digest of " + emails.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
