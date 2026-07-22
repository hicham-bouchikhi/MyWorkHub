using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>
/// <see cref="IAzureDevOpsSettingsService"/> (T065): persists the org URL + projects to
/// the user's appsettings.json and the PAT to the DPAPI credential store. The same
/// <see cref="AzureDevOpsOptions"/> instance is shared (via <see cref="IOptions{T}"/>) with
/// <see cref="AzureDevOpsService"/>, so mutating it here makes the new org/projects take
/// effect immediately — no app restart needed.
/// </summary>
public sealed class AzureDevOpsSettingsService : IAzureDevOpsSettingsService
{
    private readonly AzureDevOpsOptions _options;
    private readonly ICredentialStore _credentials;

    public AzureDevOpsSettingsService(IOptions<AzureDevOpsOptions> options, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentials);
        _options = options.Value;
        _credentials = credentials;
    }

    public AzureDevOpsSettings Get()
    {
        var hasPat = !string.IsNullOrWhiteSpace(_credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT));
        return new AzureDevOpsSettings(_options.OrganizationUrl, [.. _options.Projects], hasPat);
    }

    public void Save(string organizationUrl, IEnumerable<string> projects, string? personalAccessToken)
    {
        ArgumentNullException.ThrowIfNull(organizationUrl);
        ArgumentNullException.ThrowIfNull(projects);

        var cleanedUrl = organizationUrl.Trim();
        var cleanedProjects = projects
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (personalAccessToken is not null)
        {
            if (string.IsNullOrWhiteSpace(personalAccessToken))
            {
                _credentials.Delete(CredentialKeys.AZURE_DEVOPS_PAT);
            }
            else
            {
                _credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, personalAccessToken.Trim());
            }
        }

        // Update the live (shared) options so AzureDevOpsService sees the change at once.
        _options.OrganizationUrl = cleanedUrl;
        _options.Projects.Clear();
        foreach (var project in cleanedProjects)
        {
            _options.Projects.Add(project);
        }

        // Persist for the next launch.
        UserAppSettingsFile.SaveAzureDevOps(cleanedUrl, cleanedProjects);
    }
}
