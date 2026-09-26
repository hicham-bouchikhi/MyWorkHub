using System.Globalization;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Core.Features.PrReview;
using MyWorkHub.Infrastructure.Configuration;
using MyWorkHub.Infrastructure.Features.Workspace;

namespace MyWorkHub.Infrastructure.Features.PrReview;

/// <summary>
/// <see cref="IPrReviewService"/>: prepares the repository (<see cref="IRepositoryWorkspace"/>), then runs the
/// shared <see cref="ICliAgentRunner"/> inside the checked-out repository — the one legitimate non-isolated
/// working directory, since the review needs the code. The code under review is untrusted too, so the agent
/// gets read-only tools plus read-only git commands, with writes, network and sub-agents denied and ambient MCP
/// servers ignored.
/// </summary>
internal sealed class ClaudePrReviewService : IPrReviewService
{
    /// <summary>Pre-approved so the headless run never stalls on a permission prompt it cannot answer.</summary>
    internal static readonly IReadOnlyList<string> AllowedTools =
    [
        "Read", "Grep", "Glob",
        "Bash(git diff:*)", "Bash(git log:*)", "Bash(git show:*)", "Bash(git status:*)", "Bash(git blame:*)",
    ];

    /// <summary>Denied even if the user's own CLI settings would allow them (e.g. a permissive default mode).</summary>
    internal static readonly IReadOnlyList<string> DisallowedTools =
        ["Edit", "Write", "NotebookEdit", "WebFetch", "WebSearch", "Task", "Agent"];

    private readonly IRepositoryWorkspace _workspace;
    private readonly ReviewAgentTemplateResolver _templates;
    private readonly ICliAgentRunner _runner;
    private readonly LiveOptions<WorkspaceOptions> _options;

    public ClaudePrReviewService(
        IRepositoryWorkspace workspace, ReviewAgentTemplateResolver templates, ICliAgentRunner runner, LiveOptions<WorkspaceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(options);
        _workspace = workspace;
        _templates = templates;
        _runner = runner;
        _options = options;
    }

    public async Task<string> ReviewAsync(
        PullRequestItem pr, IProgress<string>? progress = null, string? agentFilePath = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pr);

        var repositoryDirectory = await _workspace.PrepareAsync(pr, progress, ct).ConfigureAwait(false);
        var template = _templates.Resolve(agentFilePath, progress);

        var modelId = _options.Current.ReviewModelId;
        progress?.Report(modelId is { } model
            ? $"Running the Claude review with model '{model}' — this can take a while…"
            : "Running the Claude review — this can take a while…");

        var request = new CliAgentRequest(
            template,
            BuildUserMessage(pr),
            WorkingDirectory: repositoryDirectory,
            ModelId: modelId,
            DisallowedTools: DisallowedTools,
            StrictMcpConfig: true,
            AllowedTools: AllowedTools);
        return await _runner.RunAsync(request, progress, ct).ConfigureAwait(false);
    }

    internal static string BuildUserMessage(PullRequestItem pr)
    {
        ArgumentNullException.ThrowIfNull(pr);

        return string.Create(CultureInfo.InvariantCulture,
            $"""
            Review Azure DevOps pull request !{pr.Id}.

            Project: {pr.Project}
            Repository: {pr.Repository}
            Author: {pr.Author}
            Title: {pr.Title}
            Source branch: {pr.SourceBranch} (checked out as HEAD)
            Target branch: {pr.TargetBranch} (available as origin/{pr.TargetBranch})

            The change under review is `git diff origin/{pr.TargetBranch}...HEAD`.
            """);
    }
}
