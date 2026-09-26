namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// The thin, deliberately logic-free seam over MSAL's public-client application — the one part of the
/// Microsoft 365 sign-in that cannot run in a unit test (it needs a real tenant and a browser). Everything
/// built on top (<see cref="GraphAccessTokenProvider"/>, <see cref="GraphConnectionService"/>) is tested
/// against a fake of this interface. Always requests the delegated <see cref="GraphScopes"/>.
/// </summary>
internal interface IMsalAuthenticator
{
    /// <summary>The cached account's user name, or <c>null</c> when nobody is signed in.</summary>
    Task<string?> GetAccountNameAsync(CancellationToken ct = default);

    /// <summary>An access token from the cache (refreshed silently if needed), or <c>null</c> when user interaction is required.</summary>
    Task<string?> AcquireTokenSilentAsync(CancellationToken ct = default);

    /// <summary>Interactive system-browser sign-in; returns the signed-in user name. Throws <c>MsalException</c> on failure.</summary>
    Task<string> AcquireTokenInteractiveAsync(CancellationToken ct = default);

    /// <summary>Removes every cached account.</summary>
    Task SignOutAsync(CancellationToken ct = default);
}
