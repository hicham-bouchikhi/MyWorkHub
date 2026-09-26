using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Features.Automations.Browser;
using MyWorkHub.Infrastructure.Features.Automations.Persistence;
using MyWorkHub.Infrastructure.Features.Automations.RemoteWork;
using MyWorkHub.Infrastructure.Features.Automations.Sites;
using MyWorkHub.Infrastructure.Features.Automations.Transport;

namespace MyWorkHub.Infrastructure.Features.Automations;

/// <summary>
/// Registers what the automation jobs run on: run history and remote-work plans (EF Core), the site logins
/// (credential store), the Playwright browser, and the two automations. Scheduling itself lives in the
/// Automation project. Relies on the credential store and EF Core context registered by
/// <c>AddInfrastructure</c>, and on the Graph client registered by the Graph auth module.
/// </summary>
public sealed class AutomationsInfrastructureModule : IInfrastructureModule
{
    private const string DOWNLOADS_FOLDER = "automations";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Shared clock seam; TryAdd so any feature module may declare the same need.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IAutomationLogger, AutomationLogger>();
        services.AddSingleton<IRemoteWorkPlanRepository, RemoteWorkPlanRepository>();
        services.AddSingleton<IAutomationCredentialService, AutomationCredentialService>();

        services.AddSingleton(BrowserOptions.FromConfiguration(configuration));
        services.AddSingleton<IBrowserService, PlaywrightBrowserService>();
        services.AddSingleton(ExternalSitesOptions.FromConfiguration(configuration));
        services.AddSingleton(new AutomationPaths(Path.Combine(AppPaths.TempDir, DOWNLOADS_FOLDER), AppPaths.ErrorsDir));
        services.AddSingleton<SiteScriptRunner>();

        services.AddSingleton<OutlookRemoteWorkCalendar>();
        services.AddSingleton<IRemoteWorkSyncAutomation, RemoteWorkSyncAutomation>();

        services.AddSingleton(TransportReimbursementOptions.FromConfiguration(configuration));
        services.AddSingleton<ITransportReimbursementAutomation, TransportReimbursementAutomation>();
    }
}
