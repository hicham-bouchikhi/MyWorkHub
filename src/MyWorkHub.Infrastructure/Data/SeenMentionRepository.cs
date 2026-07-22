using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Data;

/// <summary><see cref="ISeenMentionRepository"/> backed by the app's SQLite database.</summary>
public sealed class SeenMentionRepository : ISeenMentionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SeenMentionRepository(IDbContextFactory<AppDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public async Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var ids = await db.SeenMentions
            .Select(static m => m.CommentId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public async Task MarkSeenAsync(int commentId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var alreadyExists = await db.SeenMentions
            .AnyAsync(m => m.CommentId == commentId, ct)
            .ConfigureAwait(false);

        if (!alreadyExists)
        {
            db.SeenMentions.Add(new SeenWorkItemMention { CommentId = commentId, SeenAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
