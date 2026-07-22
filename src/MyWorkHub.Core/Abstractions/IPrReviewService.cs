using MyWorkHub.Core.Models;

namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Outcome of the pre-flight checks run before a review: whether <c>git</c> and the
/// <c>claude</c> CLI are runnable, and (when set) a human-readable reason the configured
/// work folder is unusable. A review can proceed only when both tools are found and
/// <see cref="WorkFolderIssue"/> is <c>null</c>.
/// </summary>
public sealed record PrerequisiteCheckResult(bool GitFound, bool ClaudeFound, string? WorkFolderIssue);

/// <summary>
/// Runs a Claude Code review of a pull request: clones/fetches the repo into the
/// configured work folder, checks out the source branch, invokes the <c>claude</c> CLI,
/// and writes a self-contained HTML report. Auth comes from the user's existing
/// <c>claude</c> session — no API key is handled here.
/// </summary>
public interface IPrReviewService
{
    /// <summary>Verifies <c>git</c>, the <c>claude</c> CLI, and the work folder are usable.</summary>
    Task<PrerequisiteCheckResult> CheckPrerequisitesAsync();

    /// <summary>
    /// Reviews <paramref name="pr"/> and returns the path to the generated HTML report.
    /// Each step (clone/fetch, checkout, running Claude, generating the report) is reported
    /// through <paramref name="progress"/> so the UI can show a live activity log. Cancelling
    /// <paramref name="ct"/> stops the run and terminates any child git/claude process.
    /// </summary>
    Task<string> ReviewAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default);
}
