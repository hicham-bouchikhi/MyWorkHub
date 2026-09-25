using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Presentation.Features.Todo;

/// <summary>Registers the Todo page and its sidebar entry.</summary>
public sealed class TodoPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 70;

    public NavigationItem? MenuEntry { get; } =
        new("Todo", "✅", typeof(TodoViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<TodoViewModel>();
    }
}
