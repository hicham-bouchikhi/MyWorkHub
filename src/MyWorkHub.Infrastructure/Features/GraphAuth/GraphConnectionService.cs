using Microsoft.Identity.Client;
using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// <see cref="IGraphConnectionService"/> over the shared <see cref="IMsalAuthenticator"/> — the same MSAL
/// application and token cache the Graph client's <see cref="GraphAccessTokenProvider"/> reads, so a
/// sign-in here is immediately visible to every Graph-backed service.
/// </summary>
internal sealed class GraphConnectionService : IGraphConnectionService
{
    private const string CANCELLED_MESSAGE = "Sign-in was cancelled.";

    private readonly IMsalAuthenticator _authenticator;

    public GraphConnectionService(IMsalAuthenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        _authenticator = authenticator;
    }

    public Task<string?> GetSignedInAccountAsync(CancellationToken ct = default)
        => _authenticator.GetAccountNameAsync(ct);

    public async Task<GraphSignInResult> SignInAsync(CancellationToken ct = default)
    {
        try
        {
            var accountName = await _authenticator.AcquireTokenInteractiveAsync(ct).ConfigureAwait(false);
            return GraphSignInResult.Success(accountName);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            return GraphSignInResult.Failure(CANCELLED_MESSAGE);
        }
        catch (MsalException ex)
        {
            return GraphSignInResult.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Azure AD registration not configured (see MsalAuthenticator).
            return GraphSignInResult.Failure(ex.Message);
        }
    }

    public Task SignOutAsync(CancellationToken ct = default) => _authenticator.SignOutAsync(ct);
}
