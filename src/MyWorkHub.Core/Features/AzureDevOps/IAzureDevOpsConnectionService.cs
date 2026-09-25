namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>
/// The user's Azure DevOps personal access token (PAT), shared by every Azure DevOps-backed page.
/// The token is kept encrypted in the credential store, never in <c>appsettings.json</c>.
/// </summary>
public interface IAzureDevOpsConnectionService
{
    /// <summary>
    /// Checks <paramref name="personalAccessToken"/> against the configured organization and, only if
    /// Azure DevOps accepts it, stores it for every Azure DevOps-backed service. Failures (rejected token,
    /// organization not configured, network) are reported in the result, not thrown.
    /// </summary>
    Task<AzureDevOpsConnectResult> ConnectAsync(string personalAccessToken, CancellationToken ct = default);

    /// <summary>Forgets the stored token.</summary>
    void Disconnect();
}

/// <summary>Outcome of <see cref="IAzureDevOpsConnectionService.ConnectAsync"/>.</summary>
/// <param name="Succeeded">Whether the token was accepted and stored.</param>
/// <param name="DisplayName">The authenticated user's display name on success.</param>
/// <param name="ErrorMessage">Why connecting failed, suitable for display, on failure.</param>
public sealed record AzureDevOpsConnectResult(bool Succeeded, string? DisplayName, string? ErrorMessage)
{
    public static AzureDevOpsConnectResult Success(string displayName) => new(true, displayName, null);

    public static AzureDevOpsConnectResult Failure(string errorMessage) => new(false, null, errorMessage);
}
