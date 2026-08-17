using MyWorkHub.Core.Abstractions;
using MyWorkHub.UI.Notifications;
using MyWorkHub.UI.Platform;
using MyWorkHub.UI.Theming;
using MyWorkHub.UI.ViewModels;
using MyWorkHub.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.DependencyInjection;

public static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Avalonia shell: the framework-agnostic presentation graph (via
    /// <see cref="PresentationServiceCollectionExtensions.AddPresentation"/>) plus the Avalonia host
    /// adapters — folder picker, theme, toast notifications — and the main window.
    /// </summary>
    public static IServiceCollection AddUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPresentation();

        // Avalonia host adapters for the Core seams the presentation layer depends on.
        services.AddSingleton<IFolderPicker, AvaloniaFolderPicker>();
        services.AddSingleton<IFilePicker, AvaloniaFilePicker>();
        services.AddSingleton<IThemeService, AvaloniaThemeService>();

        // One concrete toast service exposed through the Core interface, so the main
        // window can Attach() the overlay while everyone else only sees INotificationService.
        services.AddSingleton<ToastNotificationService>();
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<ToastNotificationService>());

        services.AddTransient(sp => new MainWindow(sp.GetRequiredService<ToastNotificationService>())
        {
            DataContext = sp.GetRequiredService<MainWindowViewModel>(),
        });

        return services;
    }
}
