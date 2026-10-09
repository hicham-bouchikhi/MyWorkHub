using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// <see cref="IGraphConnectionService"/> over the shared <see cref="IMsalAuthenticator"/> — the same MSAL
/// application and token cache the Graph client's <see cref="GraphAccessTokenProvider"/> reads, so a
/// sign-in here is immediately visible to every Graph-backed service.
/// </summary>
internal sealed partial class GraphConnectionService : IGraphConnectionService
{
    private const string CANCELLED_MESSAGE = "Sign-in was cancelled.";

    private readonly IMsalAuthenticator _authenticator;
    private readonly ILogger<GraphConnectionService> _logger;

    public GraphConnectionService(IMsalAuthenticator authenticator, ILogger<GraphConnectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(authenticator);
        ArgumentNullException.ThrowIfNull(logger);
        _authenticator = authenticator;
        _logger = logger;
    }

    public Task<string?> GetSignedInAccountAsync(CancellationToken ct = default)
        => _authenticator.GetAccountNameAsync(ct);

    public async Task<GraphSignInResult> SignInAsync(CancellationToken ct = default)
    {
        try
        {
            var accountName = await _authenticator.AcquireTokenInteractiveAsync(ct).ConfigureAwait(false);
            LogSignInSucceeded();
            return GraphSignInResult.Success(accountName);
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            LogSignInCancelled();
            return GraphSignInResult.Failure(CANCELLED_MESSAGE);
        }
        catch (MsalException ex)
        {
            LogSignInFailed(ex.ErrorCode, ex);
            return GraphSignInResult.Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Azure AD registration not configured (see MsalAuthenticator).
            LogSignInFailed("configuration", ex);
            return GraphSignInResult.Failure(ex.Message);
        }
    }

    public Task SignOutAsync(CancellationToken ct = default) => _authenticator.SignOutAsync(ct);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Microsoft 365 sign-in succeeded.")]
    private partial void LogSignInSucceeded();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Microsoft 365 sign-in was cancelled by the user.")]
    private partial void LogSignInCancelled();

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Microsoft 365 sign-in failed ({Reason}).")]
    private partial void LogSignInFailed(string? reason, Exception exception);
}
