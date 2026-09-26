using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Dev;

/// <summary>Registers the Developer page and its sidebar entry, always the last one.</summary>
public sealed class DevPresentationModule : IPresentationModule
{
    // Pinned below every feature page, like the pre-rewrite app's bottom "Developer" rail entry.
    private const int MENU_ORDER = int.MaxValue;

    public NavigationItem? MenuEntry { get; } =
        new("Developer", "🛠", typeof(DevViewModel), MENU_ORDER, IsFooter: true);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<DevViewModel>();
    }
}
