namespace MyWorkHub.Core.Abstractions;

/// <summary>Persists which work item comment @mentions the user has already opened.</summary>
public interface ISeenMentionRepository
{
    /// <summary>Returns the set of comment IDs the user has already seen.</summary>
    Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default);

    /// <summary>Records a single comment as seen. Idempotent.</summary>
    Task MarkSeenAsync(int commentId, CancellationToken ct = default);
}
