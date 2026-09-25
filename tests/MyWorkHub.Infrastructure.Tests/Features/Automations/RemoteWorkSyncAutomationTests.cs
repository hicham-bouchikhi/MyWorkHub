using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.RemoteWork;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

/// <summary>The PeopleNet/mwork browser declarations (Outlook is covered by <see cref="OutlookRemoteWorkCalendarTests"/>).</summary>
public sealed class RemoteWorkSyncAutomationTests : IDisposable
{
    private static readonly RemoteWorkPlan _plan = RemoteWorkPlan.Create(2026, 9, [2, 9]);

    private readonly SiteAutomationFixture _fixture = new();
    private readonly FakeGraphRequestAdapter _graph = SiteAutomationFixture.Graph();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _graph.Dispose();

    private RemoteWorkSyncAutomation Automation()
        => new(new OutlookRemoteWorkCalendar(_graph.Client, SiteAutomationFixture.Clock), _fixture.Runner(), _fixture.Sites);

    [Fact]
    public async Task Should_sign_in_then_mark_each_planned_day_then_save()
    {
        await Automation().SyncPeopleNetAsync(_plan, Ct);

        var session = Assert.Single(_fixture.Browser.Sessions);
        Assert.Equal(
        [
            "goto https://peoplenet.example.test/login",
            "fill #user=jdoe",
            "fill #pass=pn-secret",
            "click #login",
            "click [data-date='2026-09-02']",
            "click [data-date='2026-09-09']",
            "click #save",
        ], session.Actions);
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task Should_fail_without_opening_a_browser_when_no_login_is_saved()
    {
        _fixture.Credentials.Clear(AutomationSite.PEOPLENET);

        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation().SyncPeopleNetAsync(_plan, Ct));

        Assert.Contains("PeopleNet: no login saved", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_fixture.Browser.Sessions);
    }

    [Fact]
    public async Task Should_name_the_configuration_key_to_set_when_a_selector_is_not_configured()
    {
        // mwork has no selectors configured in the fixture (nor in the shipped appsettings.json).
        _fixture.Credentials.Save(AutomationSite.MWORK, new SiteCredentials("jdoe", "pw"));

        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation().SyncMWorkAsync(_plan, Ct));

        Assert.Contains("ExternalSites:MWork:Selectors:RemoteDayTemplate", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_fixture.Browser.Sessions);
    }

    [Fact]
    public async Task Should_require_the_date_placeholder_in_the_day_selector()
    {
        _fixture.Settings["ExternalSites:PeopleNet:Selectors:RemoteDayTemplate"] = ".day";

        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation().SyncPeopleNetAsync(_plan, Ct));

        Assert.Contains("{date}", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_fixture.Browser.Sessions);
    }

    [Fact]
    public async Task Should_not_open_a_browser_for_an_empty_plan()
    {
        await Automation().SyncPeopleNetAsync(RemoteWorkPlan.Create(2026, 9, []), Ct);

        Assert.Empty(_fixture.Browser.Sessions);
    }

    [Fact]
    public async Task Should_save_a_screenshot_and_report_it_when_the_site_does_not_behave_as_scripted()
    {
        _fixture.Browser.FailOn = "click [data-date='2026-09-09']";

        var ex = await Assert.ThrowsAsync<AutomationException>(() => Automation().SyncPeopleNetAsync(_plan, Ct));

        var session = Assert.Single(_fixture.Browser.Sessions);
        var screenshot = Path.Combine(SiteAutomationFixture.SCREENSHOTS, "PEOPLENET-20260925-083000.png");
        Assert.Equal($"screenshot {screenshot}", session.Actions[^1]);
        Assert.StartsWith("PeopleNet: Timeout 30000ms exceeded", ex.Message, StringComparison.Ordinal);
        Assert.Contains(screenshot, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pn-secret", ex.Message, StringComparison.Ordinal);
        Assert.True(session.Disposed);
    }
}
