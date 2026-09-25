using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Todo;

namespace MyWorkHub.Presentation.Features.Todo;

/// <summary>One row of the todo list. Mutable only in its completion state, which the row's checkbox shows.</summary>
public sealed partial class TodoRowViewModel : ObservableObject
{
    public TodoRowViewModel(TodoItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Id = item.Id;
        Title = item.Title;
        CreatedAt = item.CreatedAt;
        _isCompleted = item.IsCompleted;
    }

    public Guid Id { get; }

    public string Title { get; }

    public DateTime CreatedAt { get; }

    [ObservableProperty]
    private bool _isCompleted;

    /// <summary>
    /// Re-announces <see cref="IsCompleted"/> without changing it, so a checkbox the user already
    /// flipped snaps back to the persisted state after a failed save.
    /// </summary>
    internal void RefreshCompletion() => OnPropertyChanged(nameof(IsCompleted));
}
