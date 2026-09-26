using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Presentation.Features.Dashboard;

/// <summary>Registers the Dashboard page and pins it first in the sidebar (the shell lands on it).</summary>
public sealed class DashboardPresentationModule : IPresentationModule
{
    // Lowest order so the Dashboard is the first sidebar entry and the startup page.
    private const int MENU_ORDER = int.MinValue;

    public NavigationItem? MenuEntry { get; } =
        new("Dashboard", "\U0001F3E0", typeof(DashboardViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<DashboardViewModel>();
    }
}
