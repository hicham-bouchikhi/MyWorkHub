using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Todo;

/// <summary>
/// The personal task list: add, tick off and delete local todo items.
/// <para>
/// Reference deep-link target: a <see cref="NavigationTarget"/> whose <c>ElementId</c> is an item id
/// (see <see cref="TargetFor"/>) lands on this page with that row selected and
/// <see cref="HighlightedItem"/> set, which the view's <c>ScrollIntoViewBehavior</c> scrolls to and
/// flashes. A focus request that arrives before the list has loaded (first visit) is kept pending and
/// applied once <see cref="LoadCommand"/> completes.
/// </para>
/// </summary>
public sealed partial class TodoViewModel : PageViewModel, IDeepLinkTarget
{
    private const string ELEMENT_ID_FORMAT = "D";

    private readonly ITodoRepository? _repository;
    private string? _pendingFocusId;
    private bool _isLoaded;

    public TodoViewModel(ITodoRepository? repository = null)
        : base("Todo")
    {
        _repository = repository;
    }

    /// <summary>The navigation target that lands on (and highlights) the given item's row.</summary>
    public static NavigationTarget TargetFor(Guid itemId)
        => new(typeof(TodoViewModel), itemId.ToString(ELEMENT_ID_FORMAT, CultureInfo.InvariantCulture));

    /// <summary>Rows, newest first.</summary>
    public ObservableCollection<TodoRowViewModel> Items { get; } = [];

    /// <summary>False when no todo storage is registered; the page then shows a notice instead of the list.</summary>
    public bool IsAvailable => _repository is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newTitle = "";

    /// <summary>The row selected in the list (two-way bound to the ListBox).</summary>
    [ObservableProperty]
    private TodoRowViewModel? _selectedItem;

    /// <summary>
    /// The row a deep link asked to reveal. The view's scroll-into-view behavior consumes it (scrolls,
    /// flashes, then writes it back to null), so every request fires exactly once — even a repeat
    /// request for the same row.
    /// </summary>
    [ObservableProperty]
    private TodoRowViewModel? _highlightedItem;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Loads the list on first display; later calls are no-ops because this page is the only writer.</summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_repository is null || _isLoaded)
        {
            return;
        }

        try
        {
            var items = await _repository.GetAllAsync(ct);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(new TodoRowViewModel(item));
            }

            _isLoaded = true;
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not load your tasks: {ex.Message}";
            return;
        }

        ApplyPendingFocus();
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync(CancellationToken ct)
    {
        try
        {
            var item = await _repository!.AddAsync(NewTitle.Trim(), ct);
            Items.Insert(0, new TodoRowViewModel(item));
            NewTitle = "";
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not add the task: {ex.Message}";
        }
    }

    private bool CanAdd() => _repository is not null && !string.IsNullOrWhiteSpace(NewTitle);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleCompletedAsync(TodoRowViewModel row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_repository is null)
        {
            return;
        }

        var completed = !row.IsCompleted;
        try
        {
            await _repository.SetCompletedAsync(row.Id, completed, ct);
            row.IsCompleted = completed;
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.RefreshCompletion();
            ErrorMessage = $"Could not update the task: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DeleteAsync(TodoRowViewModel row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_repository is null)
        {
            return;
        }

        try
        {
            await _repository.DeleteAsync(row.Id, ct);
            Items.Remove(row);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not delete the task: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public void FocusElement(string elementId)
    {
        ArgumentNullException.ThrowIfNull(elementId);

        _pendingFocusId = elementId;
        if (_isLoaded)
        {
            ApplyPendingFocus();
        }
    }

    // Unknown or malformed ids are dropped silently: a stale notification must not break the page.
    private void ApplyPendingFocus()
    {
        if (_pendingFocusId is not { } elementId)
        {
            return;
        }

        _pendingFocusId = null;
        if (!Guid.TryParse(elementId, CultureInfo.InvariantCulture, out var id)
            || Items.FirstOrDefault(r => r.Id == id) is not { } row)
        {
            return;
        }

        SelectedItem = row;
        HighlightedItem = row;
    }
}
