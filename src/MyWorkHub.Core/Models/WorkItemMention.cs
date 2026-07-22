namespace MyWorkHub.Core.Models;

/// <summary>A comment on an active work item that @mentions the current user.</summary>
public record WorkItemMention(
    int WorkItemId,
    string WorkItemTitle,
    string WorkItemUrl,
    int CommentId,
    string AuthorDisplayName,
    DateTime CreatedAt,
    string TextSnippet);
