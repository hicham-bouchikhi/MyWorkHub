using MyWorkHub.Core.Abstractions;
using MyWorkHub.UI.Navigation;
using MyWorkHub.UI.Platform;
using MyWorkHub.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.DependencyInjection;

public static class PresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the framework-agnostic presentation graph: navigation, the shared browser
    /// launcher, the notification-center view model, and every page view model. Host-specific
    /// adapters (folder picker, toast notifications, theme) are registered by the host on top.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IBrowserLauncher, BrowserLauncher>();

        // Session-only notification history backing the top-bar bell; shared (singleton)
        // between the toast service (writer) and the shell view model (reader).
        services.AddSingleton<NotificationCenterViewModel>();

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EmailViewModel>();
        services.AddTransient<PullRequestsViewModel>();
        services.AddTransient<WorkItemsViewModel>();
        services.AddTransient<TeamsViewModel>();
        services.AddTransient<TodoViewModel>();
        services.AddTransient<RemoteWorkViewModel>();
        services.AddTransient<AutomationsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DevViewModel>();

        services.AddTransient<MainWindowViewModel>();

        return services;
    }
}
