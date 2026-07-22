using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Kiota access-token provider backed by MSAL: tries the silent (cached) path only.
/// Interactive sign-in is never triggered here — it is initiated exclusively via
/// <see cref="IGraphConnectionService.ConnectAsync"/> from the Settings page.
/// Returns an empty token when no cached account exists, which causes Graph to return 401;
/// callers handle that as "not connected" (ARCHITECTURE.md §8/§12).
/// </summary>
public sealed class MsalTokenProvider : IAccessTokenProvider
{
    private readonly IPublicClientApplication _app;
    private readonly IReadOnlyList<string> _scopes;

    public MsalTokenProvider(IPublicClientApplication app, IReadOnlyList<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(scopes);
        _app = app;
        _scopes = scopes;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new(["graph.microsoft.com"]);

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!AllowedHostsValidator.IsUrlHostValid(uri))
        {
            return string.Empty;
        }

        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        try
        {
            var silent = await _app.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return silent.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            // No cached token. Interactive sign-in is only triggered via
            // IGraphConnectionService.ConnectAsync (Settings page "Connect" button).
            return string.Empty;
        }
    }
}
