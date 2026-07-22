using System.Collections.ObjectModel;

namespace MyWorkHub.Core.Configuration;

public sealed class AzureDevOpsOptions
{
    public const string SECTION = "AzureDevOps";

    public string OrganizationUrl { get; set; } = "";

    /// <summary>AzDO team project names as they appear in the URL path: dev.azure.com/cegid/{ProjectName}.</summary>
    public Collection<string> Projects { get; } = [];
}
