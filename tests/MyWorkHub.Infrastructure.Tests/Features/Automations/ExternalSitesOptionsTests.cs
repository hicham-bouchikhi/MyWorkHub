using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Sites;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class ExternalSitesOptionsTests
{
    private static ExternalSitesOptions Read(Dictionary<string, string?> settings)
        => ExternalSitesOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    [Fact]
    public void Should_read_each_site_address_and_its_configured_selectors()
    {
        var sites = Read(new()
        {
            ["ExternalSites:MWork:BaseUrl"] = " https://mwork.example.test ",
            ["ExternalSites:MWork:Selectors:LoginButton"] = " #go ",
            ["ExternalSites:MWork:Selectors:SaveButton"] = "",
        });

        Assert.Equal(AutomationSite.MWORK, sites.MWork.Site);
        Assert.Equal(new Uri("https://mwork.example.test"), sites.MWork.BaseUrl);
        Assert.Equal("#go", sites.MWork.RequireSelector(SiteSelectorKeys.LOGIN_BUTTON));

        // Blank selector values (as shipped in appsettings.json) count as not configured.
        Assert.Throws<AutomationException>(() => sites.MWork.RequireSelector(SiteSelectorKeys.SAVE_BUTTON));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://peoplenet.example.test")]
    public void Should_treat_a_missing_or_non_web_address_as_not_configured(string url)
    {
        var sites = Read(new() { ["ExternalSites:PeopleNet:BaseUrl"] = url });

        Assert.Null(sites.PeopleNet.BaseUrl);
        var ex = Assert.Throws<AutomationException>(() => sites.PeopleNet.RequireBaseUrl());
        Assert.Contains("ExternalSites:PeopleNet:BaseUrl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_read_the_transport_site_from_its_tcl_section()
    {
        var sites = Read(new() { ["ExternalSites:TCL:BaseUrl"] = "https://tcl.example.test/" });

        Assert.Equal(AutomationSite.TCL, sites.Tcl.Site);
        Assert.Equal("ExternalSites:TCL", sites.Tcl.ConfigurationPath);
        Assert.NotNull(sites.Tcl.BaseUrl);
    }
}
