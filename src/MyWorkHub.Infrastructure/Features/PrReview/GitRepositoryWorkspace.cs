using System.ComponentModel;
using System.Globalization;
using System.Text;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Configuration;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Processes;

namespace MyWorkHub.Infrastructure.Features.PrReview;

/// <summary>Gets a pull request's code onto disk, ready for review.</summary>
internal interface IRepositoryWorkspace
{
    /// <summary>
    /// Clones (first time) or fetches the pull request's repository and checks out the tip of its source branch
    /// (detached). Returns the repository directory.
    /// </summary>
    Task<string> PrepareAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default);
}

/// <summary>
/// <see cref="IRepositoryWorkspace"/> over the <c>git</c> CLI. Repositories live under
/// <c>{WorkFolderPath}/{project}/{repository}</c> and are reused across reviews (fetch, not re-clone).
/// git authenticates with the stored Azure DevOps personal access token through an HTTP header injected via
/// <c>GIT_CONFIG_*</c> environment variables — never on the command line, and never written to the clone's
/// config.
/// </summary>
internal sealed class GitRepositoryWorkspace : IRepositoryWorkspace
{
    private const string GIT = "git";
    private const int ERROR_SNIPPET_LENGTH = 400;

    private readonly IProcessRunner _processes;
    private readonly LiveOptions<WorkspaceOptions> _workspace;
    private readonly LiveOptions<AzureDevOpsOptions> _azureDevOps;
    private readonly ICredentialStore _credentials;

    public GitRepositoryWorkspace(
        IProcessRunner processes,
        LiveOptions<WorkspaceOptions> workspace,
        LiveOptions<AzureDevOpsOptions> azureDevOps,
        ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(azureDevOps);
        ArgumentNullException.ThrowIfNull(credentials);
        _processes = processes;
        _workspace = workspace;
        _azureDevOps = azureDevOps;
        _credentials = credentials;
    }

    public async Task<string> PrepareAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pr);
        ArgumentException.ThrowIfNullOrWhiteSpace(pr.SourceBranch);

        var organization = _azureDevOps.Current.OrganizationUrl
            ?? throw new AzureDevOpsNotConnectedException(
                $"The Azure DevOps organization URL is not configured (Settings → Azure DevOps, {AzureDevOpsOptions.SECTION}:OrganizationUrl).");
        var token = _credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AzureDevOpsNotConnectedException();
        }

        var environment = AuthenticationEnvironment(token);
        // Read per review, so a work folder changed in Settings applies to the next review (existing clones stay put).
        var projectDirectory = Path.Combine(_workspace.Current.WorkFolderPath, SafeDirectoryName(pr.Project));
        var repositoryDirectory = Path.Combine(projectDirectory, SafeDirectoryName(pr.Repository));

        if (Directory.Exists(Path.Combine(repositoryDirectory, ".git")))
        {
            progress?.Report($"Fetching {pr.Repository}…");
            await GitAsync("fetch", ["fetch", "--prune", "origin"], repositoryDirectory, environment, ct).ConfigureAwait(false);
        }
        else
        {
            Directory.CreateDirectory(projectDirectory);
            var cloneUrl = new Uri(organization,
                $"{Uri.EscapeDataString(pr.Project)}/_git/{Uri.EscapeDataString(pr.Repository)}");
            progress?.Report($"Cloning {pr.Repository} into {repositoryDirectory}…");
            await GitAsync("clone", ["clone", "--no-checkout", "--", cloneUrl.AbsoluteUri, repositoryDirectory],
                projectDirectory, environment, ct).ConfigureAwait(false);
        }

        // "origin/" prefixes the branch, so a hostile branch name can never be parsed as a git option.
        progress?.Report($"Checking out {pr.SourceBranch}…");
        await GitAsync("checkout", ["checkout", "--force", "--detach", "origin/" + pr.SourceBranch],
            repositoryDirectory, environment, ct).ConfigureAwait(false);

        return repositoryDirectory;
    }

    /// <summary>Per-process git configuration (git 2.31+) carrying the Basic auth header; also forbids credential prompts.</summary>
    internal static IReadOnlyDictionary<string, string> AuthenticationEnvironment(string personalAccessToken)
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(":" + personalAccessToken));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_CONFIG_COUNT"] = "1",
            ["GIT_CONFIG_KEY_0"] = "http.extraHeader",
            ["GIT_CONFIG_VALUE_0"] = "Authorization: Basic " + basic,
        };
    }

    private static string SafeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().Trim('.');
        return safe.Length > 0 ? safe : "_";
    }

    private async Task GitAsync(
        string step, IReadOnlyList<string> arguments, string workingDirectory,
        IReadOnlyDictionary<string, string> environment, CancellationToken ct)
    {
        ProcessResult result;
        try
        {
            result = await _processes.RunAsync(
                new ProcessRequest(GIT, arguments, workingDirectory, Environment: environment), ct: ct).ConfigureAwait(false);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("git is not installed or not on PATH; it is needed to review pull requests.", ex);
        }

        if (result.ExitCode != 0)
        {
            var detail = result.StandardError.Trim();
            if (detail.Length > ERROR_SNIPPET_LENGTH)
            {
                detail = detail[..ERROR_SNIPPET_LENGTH] + "…";
            }

            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"git {step} failed (exit code {result.ExitCode}): {detail}"));
        }
    }
}
