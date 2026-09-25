namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>A work item comment that @mentions the user.</summary>
/// <param name="WorkItemId">The commented work item.</param>
/// <param name="WorkItemTitle">Its title, for display.</param>
/// <param name="WorkItemUrl">Browser URL of the work item form (where the comment is shown).</param>
/// <param name="CommentId">Azure DevOps comment id (unique across the organization); the read-state key
/// and, prefixed, the deep-link element id of the mention's row on the Work items page.</param>
/// <param name="AuthorDisplayName">Who wrote the comment.</param>
/// <param name="CreatedAt">When the comment was posted (UTC).</param>
/// <param name="TextSnippet">Plain-text excerpt of the comment.</param>
public sealed record WorkItemMention(
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemUrl,
    int CommentId,
    string AuthorDisplayName,
    DateTime CreatedAt,
    string TextSnippet);
