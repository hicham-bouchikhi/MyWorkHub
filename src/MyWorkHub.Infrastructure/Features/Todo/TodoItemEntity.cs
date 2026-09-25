namespace MyWorkHub.Infrastructure.Features.Todo;

/// <summary>
/// EF Core row of the <c>Todos</c> table. Persistence-only: the rest of the app sees the immutable
/// <see cref="Core.Features.Todo.TodoItem"/> record. The column set is exactly the one created by the
/// <c>InitialCreate</c> migration, so existing databases keep working without a new migration.
/// </summary>
internal sealed class TodoItemEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";

    // Legacy column from the pre-rewrite schema. Not surfaced by the rewritten Todo feature, but
    // kept mapped so existing rows keep their data and no destructive migration is needed.
    public DateOnly? DueDate { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
