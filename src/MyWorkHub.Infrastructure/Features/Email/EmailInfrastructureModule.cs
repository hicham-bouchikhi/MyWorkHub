using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary>Registers the Graph-backed mail service and its <c>Email</c> configuration section.</summary>
public sealed class EmailInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(EmailOptions.FromConfiguration(configuration));
        services.AddSingleton<IEmailService, GraphEmailService>();
    }
}
