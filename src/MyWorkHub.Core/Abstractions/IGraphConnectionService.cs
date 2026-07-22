namespace MyWorkHub.Core.Abstractions;

/// <summary>Result of an interactive Microsoft 365 sign-in attempt.</summary>
public sealed record GraphConnectionResult(bool Success, string? AccountName, string? ErrorMessage);

/// <summary>
/// Controls the user's Microsoft 365 (Graph) connection from the Settings page.
/// Interactive browser sign-in is ONLY triggered when <see cref="ConnectAsync"/> is explicitly
/// called — never automatically on first service use.
/// </summary>
public interface IGraphConnectionService
{
    /// <summary>
    /// Launches the system-browser interactive MSAL sign-in and returns the result.
    /// The acquired token is written to the MSAL disk cache so subsequent Graph service
    /// calls pick it up via silent acquisition.
    /// </summary>
    Task<GraphConnectionResult> ConnectAsync(CancellationToken ct = default);

    /// <summary>Removes all cached accounts, effectively signing out of Microsoft 365.</summary>
    Task DisconnectAsync();

    /// <summary>
    /// Returns the signed-in account's UPN, or <c>null</c> if no cached account exists.
    /// Does NOT trigger interactive authentication.
    /// </summary>
    Task<string?> GetConnectedAccountAsync();
}
