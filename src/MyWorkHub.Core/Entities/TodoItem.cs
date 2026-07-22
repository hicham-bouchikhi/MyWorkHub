namespace MyWorkHub.Core.Entities;

/// <summary>Personal task list item (local-only, never synced).</summary>
public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
