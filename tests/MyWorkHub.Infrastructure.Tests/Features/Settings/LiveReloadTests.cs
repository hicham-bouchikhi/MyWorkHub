using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Infrastructure.Configuration;
using MyWorkHub.Infrastructure.DependencyInjection;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.PrReview;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Settings;

/// <summary>
/// The Settings page's promise: a saved Azure DevOps / Workspace edit is used by the <em>same</em> service
/// instances on their next call — no restart, no re-registration. Everything runs over a real file-backed
/// configuration that is changed between two calls.
/// </summary>
public sealed class LiveReloadTests : IDisposable
{
    private readonly SettingsFileFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private LiveOptions<AzureDevOpsOptions> LiveAzureDevOps()
        => new(() => AzureDevOpsOptions.FromConfiguration(_fixture.Configuration));

    private LiveOptions<WorkspaceOptions> LiveWorkspace()
        => new(() => WorkspaceOptions.FromConfiguration(_fixture.Configuration));

    private static PullRequestItem Pr()
        => new(7, "Fix", "web", "Beta", "Alice", "feature/fix", "main", DateTime.UtcNow,
            PullRequestRole.REVIEWER, PullRequestVote.NONE, IsDraft: false, "https://dev.azure.com/acme/Beta/_git/web/pullrequest/7");

    [Fact]
    public async Task Should_query_the_newly_saved_organization_and_projects_on_the_next_load_when_azure_devops_settings_change()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/_apis/connectionData", StringComparison.Ordinal)
                ? StubHttpMessageHandler.Json("""{"authenticatedUser":{"id":"me","providerDisplayName":"Jane"}}""")
                : StubHttpMessageHandler.Json("""{"value":[]}"""));
        var credentials = new InMemoryCredentialStore();
        credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, "pat");
        var client = new AzureDevOpsClient(new StubHttpClientFactory(handler), LiveAzureDevOps(), credentials);
        var service = new AzureDevOpsService(client, LiveAzureDevOps(), NullLogger<AzureDevOpsService>.Instance);

        // The shipped default: an organization but no project — the user's "could not reach" state.
        var before = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetMyPullRequestsAsync(ct));
        Assert.Contains("Settings → Azure DevOps", before.Message, StringComparison.Ordinal);

        await _fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Beta"]), ct);
        handler.Requests.Clear();
        await service.GetMyPullRequestsAsync(ct);

        Assert.NotEmpty(handler.Requests);
        Assert.All(handler.Requests, r => Assert.StartsWith("https://dev.azure.com/acme/", r.Uri.AbsoluteUri, StringComparison.Ordinal));
        Assert.Contains(handler.Requests, r => r.Uri.AbsolutePath == "/acme/Beta/_apis/git/pullrequests");
    }

    [Fact]
    public async Task Should_clone_into_the_newly_saved_work_folder_on_the_next_review_when_the_work_folder_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        var processes = new FakeProcessRunner();
        var credentials = new InMemoryCredentialStore();
        credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, "pat");
        var workspace = new GitRepositoryWorkspace(processes, LiveWorkspace(), LiveAzureDevOps(), credentials);
        var firstFolder = Path.Combine(_fixture.Directory.FullName, "first");
        var secondFolder = Path.Combine(_fixture.Directory.FullName, "second");

        await _fixture.Service.SaveWorkspaceAsync(new WorkspaceSettings("", firstFolder, "", ""), ct);
        var firstClone = await workspace.PrepareAsync(Pr(), ct: ct);
        await _fixture.Service.SaveWorkspaceAsync(new WorkspaceSettings("", secondFolder, "", ""), ct);
        var secondClone = await workspace.PrepareAsync(Pr(), ct: ct);

        Assert.Equal(Path.Combine(firstFolder, "Beta", "web"), firstClone);
        Assert.Equal(Path.Combine(secondFolder, "Beta", "web"), secondClone);
        Assert.Equal(Path.Combine(secondFolder, "Beta"), processes.Requests[2].WorkingDirectory);
    }

    [Fact]
    public async Task Should_pick_up_a_hand_edit_when_the_configuration_reloads()
    {
        var workspace = LiveWorkspace();
        Assert.Equal("claude-sonnet-5", workspace.Current.ReviewModelId);

        await File.WriteAllTextAsync(
            _fixture.FilePath, """{ "Workspace": { "ReviewModelId": "claude-opus-5" } }""", TestContext.Current.CancellationToken);
        _fixture.Configuration.Reload(); // what the polling file watcher triggers in the app

        Assert.Equal("claude-opus-5", workspace.Current.ReviewModelId);
    }

    [Fact]
    public async Task Should_register_options_that_follow_saved_settings_when_composed_by_the_real_modules()
    {
        var ct = TestContext.Current.CancellationToken;
        var agent = Path.Combine(_fixture.Directory.FullName, "agent.md");
        await File.WriteAllTextAsync(agent, "CUSTOM AGENT", ct);
        using var provider = new ServiceCollection().AddInfrastructure(_fixture.Configuration).BuildServiceProvider();
        var azureDevOps = provider.GetRequiredService<LiveOptions<AzureDevOpsOptions>>();
        var workspace = provider.GetRequiredService<LiveOptions<WorkspaceOptions>>();
        var templates = provider.GetRequiredService<ReviewAgentTemplateResolver>();
        Assert.Empty(azureDevOps.Current.Projects);

        await _fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha", "Beta"]), ct);
        await _fixture.Service.SaveWorkspaceAsync(new WorkspaceSettings("/usr/bin/claude", "/src/reviews", "claude-opus-5", agent), ct);

        Assert.Equal(new Uri("https://dev.azure.com/acme/"), azureDevOps.Current.OrganizationUrl);
        Assert.Equal(["Alpha", "Beta"], azureDevOps.Current.Projects);
        Assert.Equal(new WorkspaceOptions("/usr/bin/claude", "/src/reviews", "claude-opus-5", agent), workspace.Current);
        Assert.Equal("CUSTOM AGENT", templates.Resolve(overridePath: null));
    }
}
