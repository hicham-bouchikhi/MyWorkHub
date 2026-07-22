using MyWorkHub.Core.Entities;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Local-only persistence for the personal task list (EF Core + SQLite).</summary>
public interface ITodoRepository
{
    /// <summary>All todo items, never synced to any external system.</summary>
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Adds a new todo item.</summary>
    Task AddAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Persists changes to an existing todo item (e.g. completion).</summary>
    Task UpdateAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Removes the todo item with the given id.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
