namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>
/// Thrown by an Azure DevOps-backed service when it cannot authenticate: no personal access token is
/// stored, the organization URL is not configured, or Azure DevOps rejected the stored token (expired or
/// revoked). Pages catch it to ask for a token instead of showing an error.
/// </summary>
public sealed class AzureDevOpsNotConnectedException : Exception
{
    private const string DEFAULT_MESSAGE = "No Azure DevOps personal access token is configured.";

    public AzureDevOpsNotConnectedException()
        : base(DEFAULT_MESSAGE)
    {
    }

    public AzureDevOpsNotConnectedException(string message)
        : base(message)
    {
    }

    public AzureDevOpsNotConnectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
