using MyWorkHub.Core.Models;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Reads pull requests and work items from Azure DevOps.</summary>
public interface IAzureDevOpsService
{
    /// <summary>Pull requests across configured projects where the current user is a reviewer.</summary>
    Task<IReadOnlyList<PullRequestItem>> GetPullRequestsForReviewAsync(CancellationToken ct = default);

    /// <summary>Current-sprint work items assigned to the current user.</summary>
    Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Comments on the given work items that @mention the current user.
    /// Best-effort: items whose comments cannot be fetched are silently skipped.
    /// </summary>
    Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
        IReadOnlyList<WorkItem> workItems, CancellationToken ct = default);

    /// <summary>Fetches the full description and acceptance criteria for a single work item.</summary>
    Task<WorkItemDetails?> GetWorkItemDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>Updates the description and/or acceptance criteria of a work item. Null arguments are left unchanged.</summary>
    Task UpdateWorkItemAsync(int id, string? description, string? acceptanceCriteria, CancellationToken ct = default);
}
