using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.DependencyInjection;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.Notifications;
using MyWorkHub.UI.Platform;
using MyWorkHub.UI.Theming;
using MyWorkHub.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.DependencyInjection;

public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Avalonia shell: the framework-agnostic presentation graph (via
    /// <see cref="PresentationServiceCollectionExtensions.AddPresentation"/>), the Avalonia host
    /// adapters — pickers, theme, toast notifications — the <see cref="ViewRegistry"/> merged from
    /// every discovered <see cref="IViewModule"/>, the <see cref="ViewLocator"/> and the main window.
    /// </summary>
    /// <param name="services">The container being composed.</param>
    /// <param name="moduleAssemblies">Assemblies scanned for both presentation and view modules; defaults
    /// to the Presentation and UI assemblies.</param>
    [RequiresUnreferencedCode("Discovers presentation and view modules via reflection.")]
    public static IServiceCollection AddUi(this IServiceCollection services, IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        IReadOnlyList<Assembly> assemblies = moduleAssemblies?.ToList()
            ?? [typeof(PresentationServiceCollectionExtensions).Assembly, typeof(UiServiceCollectionExtensions).Assembly];

        services.AddPresentation(assemblies);

        // Avalonia host adapters for the Core seams the presentation layer depends on.
        services.AddSingleton<IFolderPicker, AvaloniaFolderPicker>();
        services.AddSingleton<IFilePicker, AvaloniaFilePicker>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();

        // One concrete toast service exposed through the Core interface, so the main
        // window can Attach() the overlay while everyone else only sees INotificationService.
        services.AddSingleton<ToastNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<ToastNotificationService>());

        // Built eagerly so a view model mapped by two modules fails at composition time.
        services.AddSingleton(new ViewRegistry(ModuleDiscovery.Find<IViewModule>(assemblies)));
        services.AddSingleton<ViewLocator>();

        services.AddTransient(sp => new MainWindow(sp.GetRequiredService<ToastNotificationService>())
        {
            DataContext = sp.GetRequiredService<MainWindowViewModel>(),
        });

        return services;
    }
}
