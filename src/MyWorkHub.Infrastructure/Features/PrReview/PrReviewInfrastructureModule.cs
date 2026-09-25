using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core;
using MyWorkHub.Core.Features.PrReview;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Configuration;
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

        // Shared with the CliAgent module; TryAdd so either may declare the need. Re-read per use so edits made
        // on the Settings page (work folder, model, review agent) apply to the next review without a restart.
        services.TryAddSingleton(new LiveOptions<WorkspaceOptions>(() => WorkspaceOptions.FromConfiguration(configuration)));
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<IRepositoryWorkspace, GitRepositoryWorkspace>();
        services.AddSingleton(sp =>
        {
            var workspace = sp.GetRequiredService<LiveOptions<WorkspaceOptions>>();
            return new ReviewAgentTemplateResolver(() => workspace.Current.ReviewAgentPath, AppPaths.ReviewAgentPath);
        });
        services.AddSingleton<IPrReviewService, ClaudePrReviewService>();
    }
}
