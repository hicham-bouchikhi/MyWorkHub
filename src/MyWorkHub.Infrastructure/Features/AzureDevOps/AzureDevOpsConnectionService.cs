using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// <see cref="IAzureDevOpsConnectionService"/>: verifies a personal access token against the organization,
/// then stores it (encrypted) under <see cref="CredentialKeys.AZURE_DEVOPS_PAT"/>, where every
/// Azure DevOps request picks it up.
/// </summary>
internal sealed partial class AzureDevOpsConnectionService : IAzureDevOpsConnectionService
{
    private const string EMPTY_TOKEN_MESSAGE = "Enter a personal access token.";

    private readonly AzureDevOpsClient _client;
    private readonly ICredentialStore _credentials;
    private readonly ILogger<AzureDevOpsConnectionService> _logger;

    public AzureDevOpsConnectionService(AzureDevOpsClient client, ICredentialStore credentials, ILogger<AzureDevOpsConnectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _credentials = credentials;
        _logger = logger;
    }

    public bool HasStoredToken => !string.IsNullOrWhiteSpace(_credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT));

    public async Task<AzureDevOpsConnectResult> ConnectAsync(string personalAccessToken, CancellationToken ct = default)
    {
        var token = personalAccessToken?.Trim() ?? "";
        if (token.Length == 0)
        {
            return AzureDevOpsConnectResult.Failure(EMPTY_TOKEN_MESSAGE);
        }

        LogVerifyingToken();
        AzureDevOpsUser user;
        try
        {
            user = await _client.GetCurrentUserAsync(token, ct).ConfigureAwait(false);
        }
        catch (AzureDevOpsNotConnectedException ex)
        {
            LogTokenRejected(ex);
            return AzureDevOpsConnectResult.Failure(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            LogTokenCheckUnreachable(ex);
            return AzureDevOpsConnectResult.Failure($"Could not reach Azure DevOps: {ex.Message}");
        }

        // Only a token Azure DevOps accepted replaces the stored one.
        _credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, token);
        LogTokenStored();
        return AzureDevOpsConnectResult.Success(user.DisplayName);
    }

    public void Disconnect()
    {
        _credentials.Delete(CredentialKeys.AZURE_DEVOPS_PAT);
        LogDisconnected();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Verifying the Azure DevOps personal access token.")]
    private partial void LogVerifyingToken();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Azure DevOps rejected the personal access token.")]
    private partial void LogTokenRejected(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not verify the Azure DevOps token: Azure DevOps is unreachable.")]
    private partial void LogTokenCheckUnreachable(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Azure DevOps personal access token accepted and stored.")]
    private partial void LogTokenStored();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Azure DevOps personal access token removed.")]
    private partial void LogDisconnected();
}
