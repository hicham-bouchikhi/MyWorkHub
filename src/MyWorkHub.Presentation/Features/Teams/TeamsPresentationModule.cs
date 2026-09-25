using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Teams;

/// <summary>Registers the Teams page and its sidebar entry.</summary>
public sealed class TeamsPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 30;

    public NavigationItem? MenuEntry { get; } =
        new("Teams", "\U0001F4AC", typeof(TeamsViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<TeamsViewModel>();
    }
}
