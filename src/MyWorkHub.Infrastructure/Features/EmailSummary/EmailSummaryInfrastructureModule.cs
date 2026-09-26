using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.EmailSummary;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.EmailSummary;

/// <summary>
/// Registers the AI email digest. Relies on the mail service (Email module) and the shared Claude CLI runner
/// (CliAgent module).
/// </summary>
public sealed class EmailSummaryInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEmailSummaryService, ClaudeEmailSummaryService>();
    }
}
