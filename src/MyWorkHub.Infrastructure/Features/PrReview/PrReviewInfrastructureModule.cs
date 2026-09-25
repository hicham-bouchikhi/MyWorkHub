using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core;
using MyWorkHub.Core.Features.PrReview;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Processes;

namespace MyWorkHub.Infrastructure.Features.PrReview;

/// <summary>
/// Registers the AI pull request review: the git workspace, the review-agent template resolver and the
/// service. Relies on the Azure DevOps module (organization URL, stored token) and the shared Claude CLI
/// runner (CliAgent module).
/// </summary>
public sealed class PrReviewInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Shared with the CliAgent module; TryAdd so either may declare the need.
        services.TryAddSingleton(WorkspaceOptions.FromConfiguration(configuration));
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<IRepositoryWorkspace, GitRepositoryWorkspace>();
        services.AddSingleton(sp => new ReviewAgentTemplateResolver(
            sp.GetRequiredService<WorkspaceOptions>().ReviewAgentPath, AppPaths.ReviewAgentPath));
        services.AddSingleton<IPrReviewService, ClaudePrReviewService>();
    }
}
