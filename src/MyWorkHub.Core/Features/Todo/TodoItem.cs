namespace MyWorkHub.Core.Features.Todo;

/// <summary>One entry of the personal, local-only task list (never synced to any external system).</summary>
/// <param name="Id">Stable identifier; its string form is the deep-link element id of the item's row.</param>
/// <param name="Title">What needs doing.</param>
/// <param name="IsCompleted">Whether the task has been ticked off.</param>
/// <param name="CreatedAt">When the task was added (UTC); the list shows newest first.</param>
public sealed record TodoItem(Guid Id, string Title, bool IsCompleted, DateTime CreatedAt);
