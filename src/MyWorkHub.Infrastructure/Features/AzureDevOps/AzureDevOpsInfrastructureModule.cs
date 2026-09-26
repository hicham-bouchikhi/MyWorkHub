using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Configuration;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// Registers the Azure DevOps plumbing shared by the Pull requests and Work items pages: the
/// <c>AzureDevOps</c> configuration section, the pooled REST client, the PAT connection, the data service
/// and the local mention read-state. Relies on the cross-cutting credential store and EF Core context
/// registered by <c>AddInfrastructure</c>.
/// </summary>
public sealed class AzureDevOpsInfrastructureModule : IInfrastructureModule
{
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Re-read per request, so an organization URL / project list saved on the Settings page applies to the
        // next load without a restart.
        services.AddSingleton(new LiveOptions<AzureDevOpsOptions>(() => AzureDevOpsOptions.FromConfiguration(configuration)));

        // Named client from IHttpClientFactory (pooled, rotated handlers). AzureDevOpsClient asks the
        // factory per request, so it — and the services over it — can safely be singletons.
        services.AddHttpClient(AzureDevOpsClient.HTTP_CLIENT_NAME, http => http.Timeout = _requestTimeout);
        services.AddSingleton<AzureDevOpsClient>();

        services.AddSingleton<IAzureDevOpsConnectionService, AzureDevOpsConnectionService>();
        services.AddSingleton<IAzureDevOpsService, AzureDevOpsService>();

        // Shared clock seam; TryAdd so any feature module may declare the same need.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISeenMentionRepository, SeenMentionRepository>();
    }
}
