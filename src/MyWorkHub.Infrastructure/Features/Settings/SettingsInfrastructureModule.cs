using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.Settings;

/// <summary>
/// Registers the Settings page's persistence: <see cref="ISettingsService"/> reading the live configuration and
/// writing <c>~/.MyWorkHub/appsettings.json</c> (the file the configuration is loaded from).
/// </summary>
public sealed class SettingsInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Factory-registered so the container owns (and disposes) the file's write gate.
        services.AddSingleton(_ => new JsonSettingsFile(AppPaths.UserAppSettingsPath));
        services.AddSingleton<ISettingsService>(sp => new SettingsService(configuration, sp.GetRequiredService<JsonSettingsFile>()));
    }
}
