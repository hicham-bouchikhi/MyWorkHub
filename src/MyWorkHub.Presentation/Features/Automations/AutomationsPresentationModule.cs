using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Automations;

/// <summary>Registers the Automations page and its sidebar entry.</summary>
public sealed class AutomationsPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 80;

    public NavigationItem? MenuEntry { get; } =
        new("Automations", "⚙", typeof(AutomationsViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<AutomationsViewModel>();
    }
}
