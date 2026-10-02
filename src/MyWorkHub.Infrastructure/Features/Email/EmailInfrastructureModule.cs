using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Configuration;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary>
/// Registers the Graph-backed mail service and its <c>Email</c> configuration section — live, since the Settings
/// page edits it.
/// </summary>
public sealed class EmailInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new LiveOptions<EmailOptions>(() => EmailOptions.FromConfiguration(configuration)));
        services.AddSingleton<IEmailService, GraphEmailService>();
    }
}
