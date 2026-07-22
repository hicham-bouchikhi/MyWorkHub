using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Wires an authenticated <see cref="GraphServiceClient"/> over MSAL. Named to avoid
/// clashing with the SDK's own <c>Microsoft.Graph.GraphClientFactory</c>.
/// </summary>
public static class GraphServiceClientFactory
{
    public static GraphServiceClient Create(IPublicClientApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var tokenProvider = new MsalTokenProvider(app, GraphScopes.Delegated);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        return new GraphServiceClient(authProvider);
    }
}
