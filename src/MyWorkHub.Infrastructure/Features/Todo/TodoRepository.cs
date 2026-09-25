using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Features.Todo;

/// <summary>
/// <see cref="ITodoRepository"/> over EF Core + SQLite. Uses a short-lived context per call via
/// <see cref="IDbContextFactory{TContext}"/>, so the repository itself can be a singleton.
/// </summary>
internal sealed class TodoRepository : ITodoRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public TodoRepository(IDbContextFactory<AppDbContext> contextFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Set<TodoItemEntity>()
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TodoItem(t.Id, t.Title, t.IsCompleted, t.CreatedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<TodoItem> AddAsync(string title, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var entity = new TodoItemEntity
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new TodoItem(entity.Id, entity.Title, entity.IsCompleted, entity.CreatedAt);
    }

    public async Task SetCompletedAsync(Guid id, bool isCompleted, CancellationToken ct = default)
    {
        DateTime? completedAt = isCompleted ? _timeProvider.GetUtcNow().UtcDateTime : null;

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Set<TodoItemEntity>()
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.IsCompleted, isCompleted)
                    .SetProperty(t => t.CompletedAt, completedAt),
                ct)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Set<TodoItemEntity>()
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
