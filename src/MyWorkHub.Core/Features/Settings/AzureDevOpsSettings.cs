namespace MyWorkHub.Core.Features.Settings;

/// <summary>Which Azure DevOps organization and projects to read, as stored under the <c>AzureDevOps</c> section.</summary>
/// <param name="OrganizationUrl">Organization root, e.g. <c>https://dev.azure.com/your-org/</c>.</param>
/// <param name="Projects">Team projects whose pull requests are listed (work items are organization-wide).</param>
public sealed record AzureDevOpsSettings(string OrganizationUrl, IReadOnlyList<string> Projects);
