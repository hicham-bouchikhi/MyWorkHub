using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.PrReview;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.PrReview;

public sealed class ClaudePrReviewServiceTests : IDisposable
{
    private const string REPOSITORY_DIRECTORY = "/work/Alpha/web";

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("pr-review-test-");
    private readonly FakeCliAgentRunner _runner = new() { Output = "## Verdict\nApproved" };
    private readonly StubWorkspace _workspace = new();

    public void Dispose() => _directory.Delete(recursive: true);

    private static PullRequestItem Pr()
        => new(42, "Add login", "web", "Alpha", "Alice", "feature/login", "main", DateTime.UtcNow,
            PullRequestRole.REVIEWER, PullRequestVote.NONE, IsDraft: false, "https://dev.azure.com/cegid/Alpha/_git/web/pullrequest/42");

    private string Template(string name, string content)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllText(path, content);
        return path;
    }

    private ClaudePrReviewService Service(string? model = "claude-sonnet-5", string? configuredAgent = null)
        => new(_workspace,
            new ReviewAgentTemplateResolver(configuredAgent, Template("seeded.md", "SEEDED TEMPLATE")),
            _runner,
            new WorkspaceOptions("claude", "/work", model, configuredAgent));

    [Fact]
    public async Task Should_review_inside_the_checked_out_repository_with_read_only_tools()
    {
        var report = await Service().ReviewAsync(Pr(), ct: TestContext.Current.CancellationToken);

        var request = Assert.Single(_runner.Requests);
        Assert.Equal("## Verdict\nApproved", report);
        Assert.Equal(REPOSITORY_DIRECTORY, request.WorkingDirectory);
        Assert.Equal("claude-sonnet-5", request.ModelId);
        Assert.True(request.StrictMcpConfig);
        Assert.Equal(["Read", "Grep", "Glob", "Bash(git diff:*)", "Bash(git log:*)", "Bash(git show:*)", "Bash(git status:*)", "Bash(git blame:*)"], request.AllowedTools);
        Assert.Equal(["Edit", "Write", "NotebookEdit", "WebFetch", "WebSearch", "Task", "Agent"], request.DisallowedTools);
        Assert.Equal(42, Assert.Single(_workspace.Prepared).Id);
    }

    [Fact]
    public async Task Should_use_the_resolved_template_as_the_system_prompt_and_the_pr_as_the_user_message()
    {
        await Service().ReviewAsync(Pr(), ct: TestContext.Current.CancellationToken);

        var request = Assert.Single(_runner.Requests);
        Assert.Equal("SEEDED TEMPLATE", request.SystemPrompt);
        Assert.Contains("pull request !42", request.UserMessage, StringComparison.Ordinal);
        Assert.Contains("git diff origin/main...HEAD", request.UserMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("SEEDED TEMPLATE", request.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_let_a_per_review_agent_file_override_the_configured_one()
    {
        var service = Service(configuredAgent: Template("configured.md", "CONFIGURED"));

        await service.ReviewAsync(Pr(), agentFilePath: Template("override.md", "OVERRIDE"), ct: TestContext.Current.CancellationToken);
        await service.ReviewAsync(Pr(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(["OVERRIDE", "CONFIGURED"], _runner.Requests.Select(r => r.SystemPrompt));
    }

    [Fact]
    public async Task Should_keep_the_cli_default_model_when_none_is_configured()
    {
        var progress = new RecordingProgress();

        await Service(model: null).ReviewAsync(Pr(), progress, ct: TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(_runner.Requests).ModelId);
        Assert.Equal(
            ["prepared", "Using the built-in review agent (" + Path.Combine(_directory.FullName, "seeded.md") + ").", "Running the Claude review — this can take a while…"],
            progress.Messages);
    }

    [Fact]
    public async Task Should_not_run_the_agent_when_the_repository_cannot_be_prepared()
    {
        _workspace.Failure = new InvalidOperationException("git clone failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ReviewAsync(Pr(), ct: TestContext.Current.CancellationToken));

        Assert.Empty(_runner.Requests);
    }

    private sealed class StubWorkspace : IRepositoryWorkspace
    {
        public List<PullRequestItem> Prepared { get; } = [];

        public Exception? Failure { get; set; }

        public Task<string> PrepareAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Prepared.Add(pr);
            progress?.Report("prepared");
            return Task.FromResult(REPOSITORY_DIRECTORY);
        }
    }
}
