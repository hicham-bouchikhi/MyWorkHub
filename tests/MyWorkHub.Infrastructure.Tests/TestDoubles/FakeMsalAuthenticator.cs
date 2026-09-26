using MyWorkHub.Infrastructure.Features.GraphAuth;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>Scriptable stand-in for the MSAL seam: no tenant, no browser.</summary>
internal sealed class FakeMsalAuthenticator : IMsalAuthenticator
{
    public string? AccountName { get; set; }

    public string? SilentToken { get; set; }

    /// <summary>When set, the interactive sign-in throws it instead of succeeding.</summary>
    public Exception? InteractiveFailure { get; set; }

    public string InteractiveAccountName { get; set; } = "someone@cegid.com";

    public int SilentCalls { get; private set; }

    public bool SignedOut { get; private set; }

    public Task<string?> GetAccountNameAsync(CancellationToken ct = default) => Task.FromResult(AccountName);

    public Task<string?> AcquireTokenSilentAsync(CancellationToken ct = default)
    {
        SilentCalls++;
        return Task.FromResult(SilentToken);
    }

    public Task<string> AcquireTokenInteractiveAsync(CancellationToken ct = default)
    {
        if (InteractiveFailure is not null)
        {
            throw InteractiveFailure;
        }

        AccountName = InteractiveAccountName;
        return Task.FromResult(InteractiveAccountName);
    }

    public Task SignOutAsync(CancellationToken ct = default)
    {
        SignedOut = true;
        AccountName = null;
        return Task.CompletedTask;
    }
}
