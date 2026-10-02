using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
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
        services.AddTransient<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetService<IEmailService>(),
            sp.GetService<ITeamsService>(),
            sp.GetService<IAzureDevOpsService>(),
            sp.GetService<ITodoRepository>(),
            sp.GetService<ICalendarService>(),
            sp.GetRequiredService<INavigationService>()
        ));
    }
}
