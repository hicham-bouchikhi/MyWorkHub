using Microsoft.Graph.Models;
using MyWorkHub.Infrastructure.Features.Calendar;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Calendar;

public sealed class GraphCalendarServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 7, 30, 0, TimeSpan.Zero);

    private static Event EventAt(string id, string start, string end, bool? isCancelled = null) => new()
    {
        Id = id,
        Subject = "Subject " + id,
        Start = new DateTimeTimeZone { DateTime = start, TimeZone = "UTC" },
        End = new DateTimeTimeZone { DateTime = end, TimeZone = "UTC" },
        IsCancelled = isCancelled,
        Location = new Location { DisplayName = "Room 1" },
    };

    private static GraphCalendarService Service(FakeGraphRequestAdapter adapter)
        => new(adapter.Client, new FixedTimeProvider(_now));

    [Fact]
    public async Task Should_query_the_calendar_view_from_now_for_the_requested_days_in_utc()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => new EventCollectionResponse { Value = [] });

        await Service(adapter).GetUpcomingEventsAsync(7, TestContext.Current.CancellationToken);

        var request = Assert.Single(adapter.Requests);
        Assert.Equal("/me/calendarView", request.GraphPath());
        var query = request.DecodedQuery();
        Assert.Contains("startDateTime=2026-09-25T07:30:00.0000000+00:00", query, StringComparison.Ordinal);
        Assert.Contains("endDateTime=2026-10-02T07:30:00.0000000+00:00", query, StringComparison.Ordinal);
        Assert.Contains("outlook.timezone=\"UTC\"", request.Headers["Prefer"]);
    }

    [Fact]
    public async Task Should_return_non_cancelled_events_ordered_by_start_in_utc()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => new EventCollectionResponse
        {
            Value =
            [
                EventAt("late", "2026-09-26T14:00:00.0000000", "2026-09-26T15:00:00.0000000"),
                EventAt("cancelled", "2026-09-25T09:00:00.0000000", "2026-09-25T10:00:00.0000000", isCancelled: true),
                EventAt("early", "2026-09-25T08:00:00.0000000", "2026-09-25T08:30:00.0000000"),
            ],
        });

        var events = await Service(adapter).GetUpcomingEventsAsync(7, TestContext.Current.CancellationToken);

        Assert.Equal(["early", "late"], events.Select(e => e.Id));
        Assert.Equal(new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc), events[0].Start);
        Assert.Equal(DateTimeKind.Utc, events[0].Start.Kind);
        Assert.Equal("Room 1", events[0].Location);
    }

    [Fact]
    public void Should_drop_an_occurrence_whose_times_cannot_be_read()
    {
        Assert.Null(GraphCalendarService.ToCalendarEvent(EventAt("broken", "not a date", "2026-09-25T08:30:00")));
    }

    [Fact]
    public async Task Should_reject_a_non_positive_window()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => null);
        var service = Service(adapter);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetUpcomingEventsAsync(0, TestContext.Current.CancellationToken));
    }
}
