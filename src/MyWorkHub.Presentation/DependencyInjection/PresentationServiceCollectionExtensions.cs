using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.Platform;
using MyWorkHub.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Presentation.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the framework-agnostic presentation graph: navigation, the shared browser launcher,
    /// the notification-center and shell view models, and every discovered
    /// <see cref="IPresentationModule"/>. The modules' menu entries are collected — sorted by
    /// <see cref="NavigationItem.Order"/> then label — into the <c>IReadOnlyList&lt;NavigationItem&gt;</c>
    /// singleton the shell's sidebar is built from. Host-specific adapters (pickers, toast
    /// notifications, theme, views) are registered by the host on top.
    /// </summary>
    /// <param name="services">The container being composed.</param>
    /// <param name="moduleAssemblies">Assemblies to scan for modules; defaults to this (Presentation) assembly.</param>
    [RequiresUnreferencedCode("Discovers presentation modules via reflection.")]
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IBrowserLauncher, BrowserLauncher>();

        // Session-only notification history backing the top-bar bell; shared (singleton)
        // between the toast service (writer) and the shell view model (reader).
        services.AddSingleton<NotificationCenterViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var assemblies = moduleAssemblies ?? [typeof(PresentationServiceCollectionExtensions).Assembly];
        var menu = new List<NavigationItem>();
        foreach (var module in ModuleDiscovery.Find<IPresentationModule>(assemblies))
        {
            module.RegisterServices(services);
            if (module.MenuEntry is { } entry)
            {
                menu.Add(entry);
            }
        }

        IReadOnlyList<NavigationItem> sidebar = menu
            .OrderBy(i => i.Order)
            .ThenBy(i => i.Label, StringComparer.Ordinal)
            .ToList();
        services.AddSingleton(sidebar);

        return services;
    }
}
