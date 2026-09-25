using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// <see cref="IAzureDevOpsConnectionService"/>: verifies a personal access token against the organization,
/// then stores it (encrypted) under <see cref="CredentialKeys.AZURE_DEVOPS_PAT"/>, where every
/// Azure DevOps request picks it up.
/// </summary>
internal sealed class AzureDevOpsConnectionService : IAzureDevOpsConnectionService
{
    private const string EMPTY_TOKEN_MESSAGE = "Enter a personal access token.";

    private readonly AzureDevOpsClient _client;
    private readonly ICredentialStore _credentials;

    public AzureDevOpsConnectionService(AzureDevOpsClient client, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(credentials);
        _client = client;
        _credentials = credentials;
    }

    public async Task<AzureDevOpsConnectResult> ConnectAsync(string personalAccessToken, CancellationToken ct = default)
    {
        var token = personalAccessToken?.Trim() ?? "";
        if (token.Length == 0)
        {
            return AzureDevOpsConnectResult.Failure(EMPTY_TOKEN_MESSAGE);
        }

        AzureDevOpsUser user;
        try
        {
            user = await _client.GetCurrentUserAsync(token, ct).ConfigureAwait(false);
        }
        catch (AzureDevOpsNotConnectedException ex)
        {
            return AzureDevOpsConnectResult.Failure(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return AzureDevOpsConnectResult.Failure($"Could not reach Azure DevOps: {ex.Message}");
        }

        // Only a token Azure DevOps accepted replaces the stored one.
        _credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, token);
        return AzureDevOpsConnectResult.Success(user.DisplayName);
    }

    public void Disconnect() => _credentials.Delete(CredentialKeys.AZURE_DEVOPS_PAT);
}
