namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// EF Core row of the <c>SeenMentions</c> table: one mention (comment) the user has opened. The column
/// set is exactly the one created by the <c>AddSeenMentions</c> migration, so existing databases keep
/// working without a new migration.
/// </summary>
internal sealed class SeenWorkItemMentionEntity
{
    /// <summary>Azure DevOps comment id (unique across the organization) — the primary key.</summary>
    public int CommentId { get; set; }

    public DateTime SeenAt { get; set; }
}
