using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>Registers the Settings page and its sidebar entry, just above Developer.</summary>
public sealed class SettingsPresentationModule : IPresentationModule
{
    // Directly above the Developer entry (int.MaxValue), below every feature page.
    private const int MENU_ORDER = int.MaxValue - 1;

    public NavigationItem? MenuEntry { get; } =
        new("Settings", "⚙", typeof(SettingsViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<SettingsViewModel>();
    }
}
