using System.Collections.ObjectModel;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MyWorkHub.UI.ViewModels;

/// <summary>Which todo items the list shows.</summary>
public enum TodoFilter
{
    ALL,
    ACTIVE,
    COMPLETED,
}

/// <summary>
/// Personal Todo (Phase 8, T046). Fully local: talks only to <see cref="ITodoRepository"/>
/// (SQLite), never to Graph or Azure DevOps. Holds the full set in memory and projects a
/// filtered view (All / Active / Completed) into <see cref="Items"/> for the View to bind.
/// </summary>
public sealed partial class TodoViewModel : PageViewModel
{
    private readonly ITodoRepository? _repository;
    private readonly ILogger<TodoViewModel>? _logger;
    private readonly List<TodoItem> _all = [];

    [ObservableProperty]
    private string _newTitle = "";

    [ObservableProperty]
    private DateTimeOffset? _newDueDate;

    [ObservableProperty]
    private TodoFilter _filter = TodoFilter.ALL;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public TodoViewModel(ITodoRepository? repository = null, ILogger<TodoViewModel>? logger = null)
        : base("Todo")
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>The currently visible items (filtered), newest first.</summary>
    public ObservableCollection<TodoItem> Items { get; } = [];

    /// <summary>Filter options for the View's selector (All / Active / Completed).</summary>
    public IReadOnlyList<TodoFilter> Filters { get; } = Enum.GetValues<TodoFilter>();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_repository is null)
        {
            return;
        }

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var items = await _repository.GetAllAsync().ConfigureAwait(true);
            _all.Clear();
            _all.AddRange(items);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogLoadFailure(ex);
            _all.Clear();
            ApplyFilter();
            ErrorMessage = "Couldn't load your todo list.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanAdd() => !string.IsNullOrWhiteSpace(NewTitle);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        if (_repository is null || !CanAdd())
        {
            return;
        }

        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = NewTitle.Trim(),
            DueDate = NewDueDate is { } due ? DateOnly.FromDateTime(due.Date) : null,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(item).ConfigureAwait(true);
        _all.Insert(0, item);
        NewTitle = "";
        NewDueDate = null;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ToggleCompleteAsync(TodoItem? item)
    {
        if (item is null || _repository is null)
        {
            return;
        }

        item.IsCompleted = !item.IsCompleted;
        item.CompletedAt = item.IsCompleted ? DateTime.UtcNow : null;
        await _repository.UpdateAsync(item).ConfigureAwait(true);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task DeleteAsync(TodoItem? item)
    {
        if (item is null || _repository is null)
        {
            return;
        }

        await _repository.DeleteAsync(item.Id).ConfigureAwait(true);
        _all.Remove(item);
        ApplyFilter();
    }

    partial void OnNewTitleChanged(string value) => AddCommand.NotifyCanExecuteChanged();

    partial void OnFilterChanged(TodoFilter value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<TodoItem> view = Filter switch
        {
            TodoFilter.ACTIVE => _all.Where(static t => !t.IsCompleted),
            TodoFilter.COMPLETED => _all.Where(static t => t.IsCompleted),
            _ => _all,
        };

        Items.Clear();
        foreach (var item in view)
        {
            Items.Add(item);
        }
    }

    private void LogLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load the personal todo list from the local store.")]
    private static partial void LogLoadFailureCore(ILogger logger, Exception exception);
}
