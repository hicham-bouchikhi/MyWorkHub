using MyWorkHub.Core.Abstractions;
using Microsoft.Identity.Client;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Implements <see cref="IGraphConnectionService"/> using the shared
/// <see cref="IPublicClientApplication"/> singleton (same MSAL instance and DPAPI-backed token
/// cache as <see cref="MsalTokenProvider"/>), so a token acquired here is immediately visible
/// to Graph service calls via silent acquisition.
/// </summary>
public sealed class GraphConnectionService : IGraphConnectionService
{
    private readonly IPublicClientApplication _app;

    public GraphConnectionService(IPublicClientApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _app = app;
    }

    public async Task<GraphConnectionResult> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _app.AcquireTokenInteractive(GraphScopes.Delegated)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync(ct)
                .ConfigureAwait(false);

            var name = result.Account?.Username ?? result.Account?.HomeAccountId?.Identifier;
            return new GraphConnectionResult(true, name, null);
        }
        catch (OperationCanceledException)
        {
            return new GraphConnectionResult(false, null, "Sign-in was cancelled.");
        }
        catch (MsalException ex)
        {
            return new GraphConnectionResult(false, null, ex.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        foreach (var account in accounts)
        {
            await _app.RemoveAsync(account).ConfigureAwait(false);
        }
    }

    public async Task<string?> GetConnectedAccountAsync()
    {
        var accounts = await _app.GetAccountsAsync().ConfigureAwait(false);
        var first = accounts.FirstOrDefault();
        return first?.Username ?? first?.HomeAccountId?.Identifier;
    }
}
