using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Data;

/// <summary>
/// <see cref="ITodoRepository"/> over EF Core + SQLite (T045). Local-only persistence for the
/// personal task list — no network, no Graph, no Azure DevOps. Uses a short-lived context per
/// call via <see cref="IDbContextFactory{TContext}"/>, matching the app's pooled-context model.
/// </summary>
public sealed class TodoRepository : ITodoRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public TodoRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Todos
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(TodoItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Todos.Add(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TodoItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Todos.Update(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existing = await db.Todos.FindAsync([id], ct).ConfigureAwait(false);
        if (existing is not null)
        {
            db.Todos.Remove(existing);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
