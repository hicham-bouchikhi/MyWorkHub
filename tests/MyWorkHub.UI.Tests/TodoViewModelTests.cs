using MyWorkHub.Core.Entities;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class TodoViewModelTests
{
    [Fact]
    public async Task Load_populates_items_from_the_repository()
    {
        var repo = new FakeTodoRepository([Todo("a"), Todo("b")]);
        var vm = new TodoViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public async Task Add_creates_the_item_clears_the_input_and_shows_it()
    {
        var vm = new TodoViewModel(new FakeTodoRepository());
        vm.NewTitle = "  Write report  ";

        await vm.AddCommand.ExecuteAsync(null);

        var item = Assert.Single(vm.Items);
        Assert.Equal("Write report", item.Title);   // trimmed
        Assert.Equal("", vm.NewTitle);               // input cleared
        Assert.False(item.IsCompleted);
    }

    [Fact]
    public void Add_is_disabled_when_the_title_is_blank()
    {
        var vm = new TodoViewModel(new FakeTodoRepository());

        vm.NewTitle = "   ";
        Assert.False(vm.AddCommand.CanExecute(null));

        vm.NewTitle = "Something";
        Assert.True(vm.AddCommand.CanExecute(null));
    }

    [Fact]
    public async Task Add_carries_the_chosen_due_date()
    {
        var vm = new TodoViewModel(new FakeTodoRepository());
        vm.NewTitle = "Pay invoice";
        vm.NewDueDate = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 7, 1), Assert.Single(vm.Items).DueDate);
    }

    [Fact]
    public async Task ToggleComplete_marks_the_item_completed()
    {
        var item = Todo("a");
        var vm = new TodoViewModel(new FakeTodoRepository([item]));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleCompleteCommand.ExecuteAsync(item);

        Assert.True(item.IsCompleted);
        Assert.NotNull(item.CompletedAt);
    }

    [Fact]
    public async Task Delete_removes_the_item()
    {
        var item = Todo("a");
        var vm = new TodoViewModel(new FakeTodoRepository([item]));
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.DeleteCommand.ExecuteAsync(item);

        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task Active_filter_hides_completed_items()
    {
        var done = Todo("done", completed: true);
        var open = Todo("open");
        var vm = new TodoViewModel(new FakeTodoRepository([done, open]));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Filter = TodoFilter.ACTIVE;

        Assert.Equal("open", Assert.Single(vm.Items).Title);
    }

    [Fact]
    public async Task Completed_filter_shows_only_completed_items()
    {
        var done = Todo("done", completed: true);
        var open = Todo("open");
        var vm = new TodoViewModel(new FakeTodoRepository([done, open]));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Filter = TodoFilter.COMPLETED;

        Assert.Equal("done", Assert.Single(vm.Items).Title);
    }

    [Fact]
    public async Task Load_with_a_failing_repository_reports_an_error_and_stays_empty()
    {
        var vm = new TodoViewModel(new FakeTodoRepository(throwOnCall: true));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
        Assert.NotNull(vm.ErrorMessage);
    }

    private static TodoItem Todo(string title, bool completed = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        IsCompleted = completed,
        CreatedAt = DateTime.UtcNow,
    };
}
