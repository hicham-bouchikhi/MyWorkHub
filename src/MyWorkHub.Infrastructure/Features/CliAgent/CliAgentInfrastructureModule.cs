using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Configuration;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Processes;

namespace MyWorkHub.Infrastructure.Features.CliAgent;

/// <summary>
/// Registers the shared Claude CLI runner every AI feature goes through, plus the <c>Workspace</c>
/// configuration section and the process seam it needs.
/// </summary>
public sealed class CliAgentInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Shared with the pull request review module; TryAdd so either may declare the need. Re-read per use so
        // a Claude CLI path edited on the Settings page applies to the next run without a restart.
        services.TryAddSingleton(new LiveOptions<WorkspaceOptions>(() => WorkspaceOptions.FromConfiguration(configuration)));
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<ICliAgentRunner, ClaudeCliAgentRunner>();
    }
}
