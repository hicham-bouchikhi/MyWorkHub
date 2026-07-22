namespace MyWorkHub.Core.Abstractions;

/// <summary>Current Azure DevOps integration settings, projected for the Settings form.</summary>
public sealed record AzureDevOpsSettings(
    string OrganizationUrl,
    IReadOnlyList<string> Projects,
    bool HasPersonalAccessToken);

/// <summary>
/// Reads and persists the Azure DevOps integration settings (T065). The organization URL
/// and project list go to <c>appsettings.json</c>; the PAT goes to the DPAPI credential
/// store. Saving also updates the live configuration so the AzDO modules pick up the new
/// values without an app restart.
/// </summary>
public interface IAzureDevOpsSettingsService
{
    /// <summary>The current org URL, project list, and whether a PAT is stored.</summary>
    AzureDevOpsSettings Get();

    /// <summary>
    /// Persists the org URL + projects and refreshes the live configuration. When
    /// <paramref name="personalAccessToken"/> is non-null it is saved to the credential
    /// store (or cleared when blank); passing <c>null</c> leaves the existing PAT untouched.
    /// </summary>
    void Save(string organizationUrl, IEnumerable<string> projects, string? personalAccessToken);
}
