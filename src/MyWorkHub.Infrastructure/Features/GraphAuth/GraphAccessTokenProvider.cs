using Microsoft.Kiota.Abstractions.Authentication;
using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// Kiota access-token provider for the shared <c>GraphServiceClient</c> (wrapped in a
/// <see cref="BaseBearerTokenAuthenticationProvider"/>). Uses the silent MSAL path only — it never opens
/// a browser. When no token is available it throws <see cref="GraphNotConnectedException"/> before the
/// request is sent, so pages can offer a sign-in instead of surfacing a 401.
/// </summary>
internal sealed class GraphAccessTokenProvider : IAccessTokenProvider
{
    private const string GRAPH_HOST = "graph.microsoft.com";

    private readonly IMsalAuthenticator _authenticator;

    public GraphAccessTokenProvider(IMsalAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        _authenticator = authenticator;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new([GRAPH_HOST]);

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // Never hand the Microsoft 365 token to another host (e.g. a redirect or a next-page link
        // pointing elsewhere), nor over plain HTTP. An empty token means "no Authorization header".
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedHostsValidator.IsUrlHostValid(uri))
        {
            return "";
        }

        return await _authenticator.AcquireTokenSilentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new GraphNotConnectedException();
    }
}
