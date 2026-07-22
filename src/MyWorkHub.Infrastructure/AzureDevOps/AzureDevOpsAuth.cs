using System.Text;
using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>PAT-based Basic authentication for the Azure DevOps REST API (T018).</summary>
public static class AzureDevOpsAuth
{
    /// <summary>
    /// Azure DevOps Basic auth uses an empty username and the PAT as the password:
    /// <c>Basic base64(":" + pat)</c>.
    /// </summary>
    public static string BuildBasicAuthHeaderValue(string personalAccessToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(personalAccessToken);
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + personalAccessToken));
        return "Basic " + encoded;
    }

    /// <summary>Reads the PAT from the credential store, or throws a configuration error.</summary>
    public static string GetPatOrThrow(ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        var pat = credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT);
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException(
                "Azure DevOps PAT is not configured. Add it in Settings (TASKS.md T065).");
        }

        return pat;
    }
}
