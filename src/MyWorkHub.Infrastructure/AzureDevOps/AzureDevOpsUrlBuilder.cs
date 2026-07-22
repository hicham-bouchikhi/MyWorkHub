using System.Globalization;

namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>Builds the browser URLs that "Open in AzDO" actions navigate to.</summary>
public static class AzureDevOpsUrlBuilder
{
    /// <summary>e.g. <c>https://dev.azure.com/cegid/MyProject/_git/MyRepo/pullrequest/42</c>.</summary>
    public static string BuildPullRequestUrl(string organizationUrl, string project, string repository, int pullRequestId)
    {
        var org = NormalizeBase(organizationUrl);
        return $"{org}{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repository)}/pullrequest/{pullRequestId.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>HTTPS git clone URL, e.g. <c>https://dev.azure.com/cegid/MyProject/_git/MyRepo</c>.</summary>
    public static string BuildCloneUrl(string organizationUrl, string project, string repository)
    {
        var org = NormalizeBase(organizationUrl);
        return $"{org}{Uri.EscapeDataString(project)}/_git/{Uri.EscapeDataString(repository)}";
    }

    /// <summary>e.g. <c>https://dev.azure.com/cegid/MyProject/_workitems/edit/123</c>.</summary>
    public static string BuildWorkItemUrl(string organizationUrl, string project, int workItemId)
    {
        var org = NormalizeBase(organizationUrl);
        return $"{org}{Uri.EscapeDataString(project)}/_workitems/edit/{workItemId.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Guarantees exactly one trailing slash so segments concatenate cleanly.</summary>
    public static string NormalizeBase(string organizationUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationUrl);
        return organizationUrl.EndsWith('/') ? organizationUrl : organizationUrl + "/";
    }
}
