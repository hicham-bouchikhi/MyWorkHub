namespace MyWorkHub.Core.Configuration;

public sealed class ExternalSitesOptions
{
    public const string SECTION = "ExternalSites";

    public SiteOptions Tcl { get; set; } = new();
    public SiteOptions PeopleNet { get; set; } = new();
    public SiteOptions MWork { get; set; } = new();
}

public sealed class SiteOptions
{
    public string BaseUrl { get; set; } = "";
}
