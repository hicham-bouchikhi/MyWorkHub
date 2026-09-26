using Microsoft.EntityFrameworkCore;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Data;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// <see cref="ISeenMentionRepository"/> over EF Core + SQLite (<c>SeenMentions</c> table). Uses a
/// short-lived context per call, so the repository itself can be a singleton.
/// </summary>
internal sealed class SeenMentionRepository : ISeenMentionRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public SeenMentionRepository(IDbContextFactory<AppDbContext> contextFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var ids = await db.Set<SeenWorkItemMentionEntity>()
            .AsNoTracking()
            .Select(m => m.CommentId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task MarkSeenAsync(int commentId, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var seen = db.Set<SeenWorkItemMentionEntity>();

        // Keeps the first time it was opened.
        if (await seen.AnyAsync(m => m.CommentId == commentId, ct).ConfigureAwait(false))
        {
            return;
        }

        seen.Add(new SeenWorkItemMentionEntity { CommentId = commentId, SeenAt = _timeProvider.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
