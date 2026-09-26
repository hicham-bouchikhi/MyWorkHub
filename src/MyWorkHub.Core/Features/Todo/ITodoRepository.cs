namespace MyWorkHub.Core.Features.Todo;

/// <summary>Local persistence for the personal task list.</summary>
public interface ITodoRepository
{
    /// <summary>Every todo item, newest first.</summary>
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Creates a new, not-yet-completed item and returns it with its assigned id and creation time.</summary>
    Task<TodoItem> AddAsync(string title, CancellationToken ct = default);

    /// <summary>Marks the item completed or not; unknown ids are ignored.</summary>
    Task SetCompletedAsync(Guid id, bool isCompleted, CancellationToken ct = default);

    /// <summary>Removes the item; unknown ids are ignored.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
