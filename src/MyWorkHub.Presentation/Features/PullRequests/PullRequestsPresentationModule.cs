using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.PullRequests;

/// <summary>Registers the Pull requests page and its sidebar entry.</summary>
public sealed class PullRequestsPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 40;

    public NavigationItem? MenuEntry { get; } =
        new("Pull requests", "\U0001F500", typeof(PullRequestsViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<PullRequestsViewModel>();
    }
}
