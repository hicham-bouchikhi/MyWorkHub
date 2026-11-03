using MyWorkHub.Core.Features.Todo;

namespace MyWorkHub.UI.Tests.Features.Todo;

/// <summary>In-memory <see cref="ITodoRepository"/>; set <see cref="Failure"/> to make every call throw.</summary>
internal sealed class FakeTodoRepository : ITodoRepository
{
    private readonly List<TodoItem> _items;
    private int _clock;

    public FakeTodoRepository(params TodoItem[] items)
    {
        _items = [.. items];
    }

    public Exception? Failure { get; set; }

    /// <summary>When set, <see cref="GetAllAsync"/> waits for it (or for its token) before answering.</summary>
    public TaskCompletionSource? GetAllGate { get; set; }

    public IReadOnlyList<TodoItem> Items => _items;

    public static TodoItem Item(string title, bool isCompleted = false)
        => new(Guid.NewGuid(), title, isCompleted, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        if (GetAllGate is { } gate)
        {
            await gate.Task.WaitAsync(ct);
        }

        ThrowIfFailing();
        return [.. _items];
    }

    public Task<TodoItem> AddAsync(string title, CancellationToken ct = default)
    {
        ThrowIfFailing();
        var item = new TodoItem(Guid.NewGuid(), title, false, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc).AddMinutes(++_clock));
        _items.Insert(0, item);
        return Task.FromResult(item);
    }

    /// <summary>When set, the next <see cref="SetCompletedAsync"/> call waits for it (or for its token) first.</summary>
    public TaskCompletionSource? SetCompletedGate { get; set; }

    public async Task SetCompletedAsync(Guid id, bool isCompleted, CancellationToken ct = default)
    {
        if (SetCompletedGate is { } gate)
        {
            SetCompletedGate = null;
            await gate.Task.WaitAsync(ct);
        }

        ThrowIfFailing();
        var index = _items.FindIndex(t => t.Id == id);
        if (index >= 0)
        {
            _items[index] = _items[index] with { IsCompleted = isCompleted };
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        ThrowIfFailing();
        _items.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}
