namespace MyWorkHub.Core.Entities;

/// <summary>Tracks which work item comment @mentions the user has already opened.</summary>
public sealed class SeenWorkItemMention
{
    /// <summary>Azure DevOps comment ID (unique per organisation — used as PK).</summary>
    public int CommentId { get; set; }

    public DateTime SeenAt { get; set; }
}
