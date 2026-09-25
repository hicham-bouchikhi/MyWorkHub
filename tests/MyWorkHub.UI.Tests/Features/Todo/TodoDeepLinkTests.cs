using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Presentation.DependencyInjection;
using MyWorkHub.Presentation.Features.Todo;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests.Features.Todo;

/// <summary>
/// End-to-end deep-link path through the real composition: NavigationTarget(TodoViewModel, itemId)
/// → NavigationService → TodoViewModel.FocusElement → row selected + highlighted.
/// </summary>
public sealed class TodoDeepLinkTests
{
    private static ServiceProvider Compose(FakeTodoRepository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITodoRepository>(repository);
        services.AddUi();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_land_on_the_todo_page_with_the_item_selected_when_navigating_before_first_load()
    {
        var target = FakeTodoRepository.Item("Target");
        using var provider = Compose(new FakeTodoRepository(FakeTodoRepository.Item("Other"), target));
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateTo(TodoViewModel.TargetFor(target.Id));
        var page = Assert.IsType<TodoViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null); // what TodoView does when it appears

        Assert.Equal(target.Id, page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }

    [Fact]
    public async Task Should_focus_the_item_on_the_cached_page_when_navigating_back_with_a_target()
    {
        var target = FakeTodoRepository.Item("Target");
        using var provider = Compose(new FakeTodoRepository(target, FakeTodoRepository.Item("Other")));
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.NavigateTo(new(typeof(TodoViewModel)));
        var page = Assert.IsType<TodoViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.Null(page.SelectedItem);

        navigation.NavigateTo(TodoViewModel.TargetFor(target.Id));

        Assert.Same(page, navigation.CurrentPage);
        Assert.Equal(target.Id, page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }

    [Fact]
    public void Should_contribute_a_sidebar_entry_and_a_view_in_the_default_composition()
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetRequiredService<IReadOnlyList<Core.Navigation.NavigationItem>>(),
            i => i.ViewModelType == typeof(TodoViewModel));
        Assert.Equal(typeof(UI.Features.Todo.TodoView),
            provider.GetRequiredService<ViewRegistry>().GetViewType(typeof(TodoViewModel)));
        ShellCompositionValidator.Validate(services, provider);
    }

    [Fact]
    public void Should_register_the_page_through_its_presentation_module_only()
    {
        // The module is discovered from the Presentation assembly; no shared file lists it.
        var services = new ServiceCollection();
        services.AddPresentation([typeof(TodoPresentationModule).Assembly]);

        Assert.Contains(services, d => d.ServiceType == typeof(TodoViewModel));
    }
}
