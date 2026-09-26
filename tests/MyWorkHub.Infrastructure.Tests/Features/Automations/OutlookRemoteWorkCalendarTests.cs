using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.RemoteWork;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

public sealed class OutlookRemoteWorkCalendarTests
{
    private static readonly RemoteWorkPlan _plan = RemoteWorkPlan.Create(2026, 9, [2, 9]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static OutlookRemoteWorkCalendar Calendar(FakeGraphRequestAdapter graph)
        => new(graph.Client, SiteAutomationFixture.Clock);

    private static Event AllDay(string subject, string date) => new()
    {
        Subject = subject,
        IsAllDay = true,
        Start = new DateTimeTimeZone { DateTime = date + "T00:00:00.0000000", TimeZone = "UTC" },
    };

    [Fact]
    public async Task Should_read_the_plan_month_in_the_local_time_zone()
    {
        using var graph = SiteAutomationFixture.Graph();

        await Calendar(graph).AddMissingDaysAsync(_plan, Ct);

        var read = graph.Requests[0];
        Assert.Equal("/me/calendarView", read.GraphPath());
        Assert.Contains("startDateTime=2026-09-01T00:00:00", read.DecodedQuery(), StringComparison.Ordinal);
        Assert.Contains("endDateTime=2026-10-01T00:00:00", read.DecodedQuery(), StringComparison.Ordinal);
        Assert.Contains("outlook.timezone=\"UTC\"", read.Headers["Prefer"]);
    }

    [Fact]
    public async Task Should_create_an_all_day_working_elsewhere_event_for_each_planned_day()
    {
        using var graph = SiteAutomationFixture.Graph();

        var created = await Calendar(graph).AddMissingDaysAsync(_plan, Ct);

        Assert.Equal(2, created);
        var writes = graph.Requests.Where(r => r.HttpMethod == Method.POST).ToList();
        Assert.Equal(2, writes.Count);
        Assert.All(writes, w => Assert.Equal("/me/events", w.GraphPath()));

        var body = SiteAutomationFixture.Body(writes[0]);
        Assert.Contains("\"subject\":\"Remote work\"", body, StringComparison.Ordinal);
        Assert.Contains("\"isAllDay\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"showAs\":\"workingElsewhere\"", body, StringComparison.Ordinal);
        Assert.Contains("\"isReminderOn\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"dateTime\":\"2026-09-02T00:00:00\"", body, StringComparison.Ordinal);
        Assert.Contains("\"dateTime\":\"2026-09-03T00:00:00\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_days_that_already_have_a_remote_work_event()
    {
        using var graph = SiteAutomationFixture.Graph(
            AllDay("Remote work", "2026-09-02"),
            AllDay("Holiday", "2026-09-09"));

        var created = await Calendar(graph).AddMissingDaysAsync(_plan, Ct);

        Assert.Equal(1, created);
        var write = Assert.Single(graph.Requests, r => r.HttpMethod == Method.POST);
        Assert.Contains("2026-09-09T00:00:00", SiteAutomationFixture.Body(write), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_call_graph_for_an_empty_plan()
    {
        using var graph = SiteAutomationFixture.Graph();

        await Calendar(graph).AddMissingDaysAsync(RemoteWorkPlan.Create(2026, 9, []), Ct);

        Assert.Empty(graph.Requests);
    }

    [Fact]
    public void Should_convert_an_iana_zone_to_the_windows_name_graph_expects()
    {
        var paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

        Assert.Equal("Romance Standard Time", OutlookRemoteWorkCalendar.GraphTimeZoneId(paris));
    }
}
