using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.Teams;

/// <summary>Registers the Graph-backed Teams chat service. The feature has no configuration section.</summary>
public sealed class TeamsInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITeamsService, GraphTeamsService>();
    }
}
