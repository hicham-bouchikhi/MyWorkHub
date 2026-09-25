namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>Local record of which work item comment @mentions the user has already opened.</summary>
public interface ISeenMentionRepository
{
    /// <summary>The comment ids of every mention already opened.</summary>
    Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default);

    /// <summary>Records the mention's comment as opened. Idempotent.</summary>
    Task MarkSeenAsync(int commentId, CancellationToken ct = default);
}
