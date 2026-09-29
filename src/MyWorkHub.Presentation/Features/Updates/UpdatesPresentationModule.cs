using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Presentation.Features.Updates;

/// <summary>Registers the app-wide <see cref="UpdateWatcher"/> (no page, no sidebar entry); the host starts it.</summary>
public sealed class UpdatesPresentationModule : IPresentationModule
{
    public NavigationItem? MenuEntry => null;

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<UpdateWatcher>();
    }
}
