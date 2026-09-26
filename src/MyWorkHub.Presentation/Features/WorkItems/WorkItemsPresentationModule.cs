using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.WorkItems;

/// <summary>Registers the Work items page and its sidebar entry.</summary>
public sealed class WorkItemsPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 50;

    public NavigationItem? MenuEntry { get; } =
        new("Work items", "\U0001F4CB", typeof(WorkItemsViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<WorkItemsViewModel>();
    }
}
