namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>
/// Read access to the user's Azure DevOps organization (configuration section <c>AzureDevOps</c>),
/// shared by the Pull requests and Work items pages. Every call throws
/// <see cref="AzureDevOpsNotConnectedException"/> when no usable personal access token is stored.
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Active pull requests the user created or is a reviewer of, across the configured projects
    /// (<c>AzureDevOps:Projects</c>), newest first. A pull request the user both created and reviews is
    /// listed once, as <see cref="PullRequestRole.AUTHOR"/>.
    /// </summary>
    Task<IReadOnlyList<PullRequestItem>> GetMyPullRequestsAsync(CancellationToken ct = default);

    /// <summary>Open work items assigned to the user across the whole organization, most recently changed first.</summary>
    Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Comments on <paramref name="workItems"/> that @mention the user, newest first. Best-effort: the
    /// comments endpoint is a preview-only API, so a work item whose comments cannot be fetched is
    /// skipped rather than failing the whole call. Items without a <see cref="WorkItem.Project"/> are skipped.
    /// </summary>
    Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
        IReadOnlyList<WorkItem> workItems, CancellationToken ct = default);
}
