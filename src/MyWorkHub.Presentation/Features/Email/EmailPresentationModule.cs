using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>Registers the Email page and its sidebar entry.</summary>
public sealed class EmailPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 10;

    public NavigationItem? MenuEntry { get; } =
        new("Email", "✉", typeof(EmailViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Transient is enough: the navigation service caches one page instance for the app's lifetime.
        services.AddTransient<EmailViewModel>();
    }
}
