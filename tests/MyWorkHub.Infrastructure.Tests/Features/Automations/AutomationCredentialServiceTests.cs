using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class AutomationCredentialServiceTests
{
    private readonly InMemoryCredentialStore _store = new();
    private readonly AutomationCredentialService _service;

    public AutomationCredentialServiceTests()
    {
        _service = new AutomationCredentialService(_store);
    }

    [Theory]
    [InlineData(AutomationSite.TCL, "TCL_USER", "TCL_PASSWORD")]
    [InlineData(AutomationSite.PEOPLENET, "PEOPLENET_USER", "PEOPLENET_PASSWORD")]
    [InlineData(AutomationSite.MWORK, "MWORK_USER", "MWORK_PASSWORD")]
    public void Should_store_each_site_login_under_the_established_credential_keys(AutomationSite site, string userKey, string passwordKey)
    {
        _service.Save(site, new SiteCredentials(" jdoe ", "s3cret"));

        Assert.Equal("jdoe", _store.Values[userKey]);
        Assert.Equal("s3cret", _store.Values[passwordKey]);
        Assert.Equal(new SiteCredentials("jdoe", "s3cret"), _service.Get(site));
    }

    [Fact]
    public void Should_report_no_login_when_only_half_of_it_is_stored()
    {
        _store.Values["PEOPLENET_USER"] = "jdoe";

        Assert.Null(_service.Get(AutomationSite.PEOPLENET));
    }

    [Fact]
    public void Should_forget_both_values_when_cleared()
    {
        _service.Save(AutomationSite.MWORK, new SiteCredentials("jdoe", "s3cret"));

        _service.Clear(AutomationSite.MWORK);

        Assert.Empty(_store.Values);
        Assert.Null(_service.Get(AutomationSite.MWORK));
    }

    [Theory]
    [InlineData("", "pw")]
    [InlineData("user", " ")]
    public void Should_reject_a_blank_user_name_or_password(string userName, string password)
    {
        Assert.Throws<ArgumentException>(() => _service.Save(AutomationSite.TCL, new SiteCredentials(userName, password)));
        Assert.Empty(_store.Values);
    }

    [Fact]
    public void Should_keep_the_password_out_of_the_login_text_form()
    {
        Assert.DoesNotContain("s3cret", new SiteCredentials("jdoe", "s3cret").ToString(), StringComparison.Ordinal);
    }
}
