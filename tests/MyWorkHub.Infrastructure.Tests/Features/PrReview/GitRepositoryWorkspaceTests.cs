using System.ComponentModel;
using System.Text;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.PrReview;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Processes;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.PrReview;

public sealed class GitRepositoryWorkspaceTests : IDisposable
{
    private const string PAT = "secret-pat";

    private readonly DirectoryInfo _workFolder = Directory.CreateTempSubdirectory("git-workspace-test-");
    private readonly FakeProcessRunner _processes = new();
    private readonly InMemoryCredentialStore _credentials = new();

    public GitRepositoryWorkspaceTests()
    {
        _credentials.Save(AzureDevOpsClient.PAT_CREDENTIAL_KEY, PAT);
    }

    public void Dispose() => _workFolder.Delete(recursive: true);

    private static PullRequestItem Pr(string project = "My Project", string repository = "web", string source = "feature/login")
        => new(42, "Login", repository, project, "Alice", source, "main", DateTime.UtcNow,
            PullRequestRole.REVIEWER, PullRequestVote.NONE, IsDraft: false, "https://dev.azure.com/cegid/x/_git/web/pullrequest/42");

    private GitRepositoryWorkspace Workspace(string? organization = "https://dev.azure.com/cegid/")
        => new(_processes,
            new WorkspaceOptions("claude", _workFolder.FullName, ReviewModelId: null, ReviewAgentPath: null),
            new AzureDevOpsOptions(organization is null ? null : new Uri(organization), []),
            _credentials);

    private string RepositoryDirectory => Path.Combine(_workFolder.FullName, "My Project", "web");

    [Fact]
    public async Task Should_clone_then_check_out_the_source_branch_detached_on_first_review()
    {
        var directory = await Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(RepositoryDirectory, directory);
        Assert.Collection(_processes.Requests,
            clone =>
            {
                Assert.Equal("git", clone.FileName);
                Assert.Equal(["clone", "--no-checkout", "--", "https://dev.azure.com/cegid/My%20Project/_git/web", RepositoryDirectory], clone.Arguments);
                Assert.Equal(Path.Combine(_workFolder.FullName, "My Project"), clone.WorkingDirectory);
            },
            checkout =>
            {
                Assert.Equal(["checkout", "--force", "--detach", "origin/feature/login"], checkout.Arguments);
                Assert.Equal(RepositoryDirectory, checkout.WorkingDirectory);
            });
    }

    [Fact]
    public async Task Should_fetch_instead_of_cloning_when_the_repository_is_already_there()
    {
        Directory.CreateDirectory(Path.Combine(RepositoryDirectory, ".git"));

        await Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken);

        Assert.Equal(["fetch", "--prune", "origin"], _processes.Requests[0].Arguments);
        Assert.Equal(RepositoryDirectory, _processes.Requests[0].WorkingDirectory);
        Assert.Equal("checkout", _processes.Requests[1].Arguments[0]);
    }

    [Fact]
    public async Task Should_authenticate_through_the_environment_and_never_put_the_token_on_the_command_line()
    {
        await Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken);

        var expectedHeader = "Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + PAT));
        Assert.All(_processes.Requests, request =>
        {
            Assert.DoesNotContain(request.Arguments, a => a.Contains(PAT, StringComparison.Ordinal) || a.Contains("Authorization", StringComparison.Ordinal));
            Assert.NotNull(request.Environment);
            Assert.Equal("http.extraHeader", request.Environment["GIT_CONFIG_KEY_0"]);
            Assert.Equal(expectedHeader, request.Environment["GIT_CONFIG_VALUE_0"]);
            Assert.Equal("0", request.Environment["GIT_TERMINAL_PROMPT"]);
        });
    }

    [Fact]
    public async Task Should_keep_a_hostile_branch_name_from_being_read_as_a_git_option()
    {
        await Workspace().PrepareAsync(Pr(source: "--upload-pack=touch pwned"), ct: TestContext.Current.CancellationToken);

        Assert.Equal("origin/--upload-pack=touch pwned", _processes.Requests[^1].Arguments[^1]);
    }

    [Fact]
    public async Task Should_sanitize_project_and_repository_names_into_directory_names()
    {
        var directory = await Workspace().PrepareAsync(Pr(project: "..", repository: "a/b"), ct: TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_workFolder.FullName, "_", "a_b"), directory);
    }

    [Fact]
    public async Task Should_require_a_stored_token_and_an_organization()
    {
        await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => Workspace(organization: null).PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken));

        _credentials.Delete(AzureDevOpsClient.PAT_CREDENTIAL_KEY);
        await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken));

        Assert.Empty(_processes.Requests);
    }

    [Fact]
    public async Task Should_stop_with_git_stderr_when_a_step_fails()
    {
        _processes.Respond = r => r.Arguments[0] == "clone"
            ? new ProcessResult(128, "", "fatal: Authentication failed")
            : new ProcessResult(0, "", "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken));

        Assert.Contains("git clone failed (exit code 128): fatal: Authentication failed", ex.Message, StringComparison.Ordinal);
        Assert.Single(_processes.Requests); // no checkout after a failed clone
    }

    [Fact]
    public async Task Should_explain_when_git_is_not_installed()
    {
        _processes.Respond = _ => throw new Win32Exception("No such file or directory");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Workspace().PrepareAsync(Pr(), ct: TestContext.Current.CancellationToken));

        Assert.Contains("git is not installed", ex.Message, StringComparison.Ordinal);
    }
}
