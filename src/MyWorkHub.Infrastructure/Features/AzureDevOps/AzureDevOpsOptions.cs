using Microsoft.Extensions.Configuration;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// Which Azure DevOps organization and projects to read (configuration section <c>AzureDevOps</c>).
/// Read key by key rather than through the reflection-based configuration binder.
/// </summary>
/// <param name="OrganizationUrl">Organization root, always ending in <c>/</c> (e.g.
/// <c>https://dev.azure.com/cegid/</c>); null when missing or not an absolute http(s) URL.</param>
/// <param name="Projects">Team projects whose pull requests are listed, de-duplicated. Work items are
/// queried organization-wide and do not depend on this list.</param>
internal sealed record AzureDevOpsOptions(Uri? OrganizationUrl, IReadOnlyList<string> Projects)
{
    public const string SECTION = "AzureDevOps";

    public static AzureDevOpsOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);

        var rawUrl = section[nameof(OrganizationUrl)]?.Trim() ?? "";
        if (rawUrl.Length > 0 && !rawUrl.EndsWith('/'))
        {
            rawUrl += "/";
        }

        var organizationUrl = Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
                              && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
            ? parsed
            : null;

        List<string> projects = [.. section.GetSection(nameof(Projects)).GetChildren()
            .Select(c => c.Value?.Trim())
            .OfType<string>()
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new AzureDevOpsOptions(organizationUrl, projects);
    }
}
