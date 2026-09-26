using MyWorkHub.Presentation.Features.Todo;

namespace MyWorkHub.UI.Tests.Features.Todo;

public sealed class TodoViewModelTests
{
    private static async Task<TodoViewModel> LoadedAsync(FakeTodoRepository repository)
    {
        var viewModel = new TodoViewModel(repository);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // --- Loading ---------------------------------------------------------------------

    [Fact]
    public async Task Should_show_the_stored_items_when_loaded()
    {
        var repository = new FakeTodoRepository(FakeTodoRepository.Item("A"), FakeTodoRepository.Item("B", isCompleted: true));

        var viewModel = await LoadedAsync(repository);

        Assert.Equal(["A", "B"], viewModel.Items.Select(r => r.Title));
        Assert.Equal([false, true], viewModel.Items.Select(r => r.IsCompleted));
    }

    [Fact]
    public async Task Should_show_an_error_when_loading_fails()
    {
        var repository = new FakeTodoRepository { Failure = new InvalidOperationException("disk gone") };

        var viewModel = await LoadedAsync(repository);

        Assert.Empty(viewModel.Items);
        Assert.Contains("disk gone", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_degrade_gracefully_when_no_repository_is_registered()
    {
        var viewModel = new TodoViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.NewTitle = "Anything";

        Assert.False(viewModel.IsAvailable);
        Assert.Empty(viewModel.Items);
        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    // --- Add / toggle / delete ----------------------------------------------------------

    [Fact]
    public async Task Should_insert_the_new_item_first_and_clear_the_input_when_adding()
    {
        var repository = new FakeTodoRepository(FakeTodoRepository.Item("Existing"));
        var viewModel = await LoadedAsync(repository);

        viewModel.NewTitle = "  Fresh  ";
        await viewModel.AddCommand.ExecuteAsync(null);

        Assert.Equal(["Fresh", "Existing"], viewModel.Items.Select(r => r.Title));
        Assert.Equal("Fresh", repository.Items[0].Title);
        Assert.Equal("", viewModel.NewTitle);
    }

    [Fact]
    public async Task Should_disable_add_when_the_title_is_blank()
    {
        var viewModel = await LoadedAsync(new FakeTodoRepository());

        viewModel.NewTitle = "   ";

        Assert.False(viewModel.AddCommand.CanExecute(null));
    }

    [Fact]
    public async Task Should_persist_and_show_completion_when_toggling()
    {
        var repository = new FakeTodoRepository(FakeTodoRepository.Item("Task"));
        var viewModel = await LoadedAsync(repository);
        var row = viewModel.Items[0];

        await viewModel.ToggleCompletedCommand.ExecuteAsync(row);

        Assert.True(row.IsCompleted);
        Assert.True(repository.Items[0].IsCompleted);
    }

    [Fact]
    public async Task Should_keep_the_row_unchanged_and_show_an_error_when_toggling_fails()
    {
        var repository = new FakeTodoRepository(FakeTodoRepository.Item("Task"));
        var viewModel = await LoadedAsync(repository);
        var row = viewModel.Items[0];
        var announced = false;
        row.PropertyChanged += (_, e) => announced |= e.PropertyName == nameof(TodoRowViewModel.IsCompleted);
        repository.Failure = new InvalidOperationException("locked");

        await viewModel.ToggleCompletedCommand.ExecuteAsync(row);

        Assert.False(row.IsCompleted);
        Assert.True(announced); // lets a checkbox the user already flipped snap back
        Assert.Contains("locked", viewModel.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_remove_the_row_when_deleting()
    {
        var repository = new FakeTodoRepository(FakeTodoRepository.Item("Keep"), FakeTodoRepository.Item("Drop"));
        var viewModel = await LoadedAsync(repository);

        await viewModel.DeleteCommand.ExecuteAsync(viewModel.Items[1]);

        Assert.Equal(["Keep"], viewModel.Items.Select(r => r.Title));
        Assert.Equal(["Keep"], repository.Items.Select(t => t.Title));
    }

    // --- Deep links (IDeepLinkTarget) ---------------------------------------------------

    [Fact]
    public async Task Should_select_and_highlight_the_row_when_focusing_a_loaded_item()
    {
        var target = FakeTodoRepository.Item("Target");
        var viewModel = await LoadedAsync(new FakeTodoRepository(FakeTodoRepository.Item("Other"), target));

        viewModel.FocusElement(target.Id.ToString());

        Assert.Equal(target.Id, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Fact]
    public async Task Should_apply_a_focus_request_made_before_loading_once_the_list_loads()
    {
        // First visit: navigation asks for the element before the view has triggered the load.
        var target = FakeTodoRepository.Item("Target");
        var viewModel = new TodoViewModel(new FakeTodoRepository(FakeTodoRepository.Item("Other"), target));

        viewModel.FocusElement(target.Id.ToString());
        Assert.Null(viewModel.SelectedItem);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(target.Id, viewModel.SelectedItem?.Id);
        Assert.Same(viewModel.SelectedItem, viewModel.HighlightedItem);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Should_ignore_a_focus_request_for_an_unknown_element(string elementId)
    {
        var viewModel = await LoadedAsync(new FakeTodoRepository(FakeTodoRepository.Item("Only")));

        viewModel.FocusElement(elementId);

        Assert.Null(viewModel.SelectedItem);
        Assert.Null(viewModel.HighlightedItem);
    }

    [Fact]
    public void Should_build_a_navigation_target_whose_element_id_is_the_item_id()
    {
        var id = Guid.NewGuid();

        var target = TodoViewModel.TargetFor(id);

        Assert.Equal(typeof(TodoViewModel), target.ViewModelType);
        Assert.Equal(id, Guid.Parse(target.ElementId!));
    }
}
