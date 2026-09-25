using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.CliAgent;

namespace MyWorkHub.Core.Features.PrReview;

/// <summary>
/// AI code review of an Azure DevOps pull request: the repository is cloned (or fetched) into the configured
/// work folder, the source branch is checked out, and the local agent CLI (<see cref="ICliAgentRunner"/>)
/// reviews it against the target branch following a review-agent Markdown template.
/// </summary>
public interface IPrReviewService
{
    /// <summary>Reviews <paramref name="pr"/> and returns the Markdown report.</summary>
    /// <param name="pr">The pull request to review.</param>
    /// <param name="progress">Receives human-readable progress (git steps, then the agent's output).</param>
    /// <param name="agentFilePath">One-off override of the review-agent template for this review only. Template
    /// resolution order: this override → the configured <c>Workspace:ReviewAgentPath</c> → the seeded built-in
    /// default → a hardcoded fallback. A path that does not resolve is skipped with a progress warning.</param>
    /// <param name="ct">Cancels the git steps and the agent run.</param>
    /// <exception cref="CliAgentException">The agent CLI is missing or failed.</exception>
    /// <exception cref="AzureDevOpsNotConnectedException">No organization URL or personal access token is configured.</exception>
    Task<string> ReviewAsync(
        PullRequestItem pr, IProgress<string>? progress = null, string? agentFilePath = null, CancellationToken ct = default);
}
