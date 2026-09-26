using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// Cross-cutting Microsoft 365 plumbing shared by the Email, Calendar and Teams slices: the Azure AD
/// options, the MSAL authenticator, and the single authenticated <see cref="GraphServiceClient"/> their
/// services consume. Not a page, so it has no presentation or view module.
/// </summary>
public sealed class GraphAuthInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(AzureAdOptions.FromConfiguration(configuration));
        services.AddSingleton<IMsalAuthenticator, MsalAuthenticator>();
        services.AddSingleton<IGraphConnectionService, GraphConnectionService>();
        services.AddSingleton(sp => new GraphServiceClient(
            new BaseBearerTokenAuthenticationProvider(
                new GraphAccessTokenProvider(sp.GetRequiredService<IMsalAuthenticator>()))));
    }
}
