using MyWorkHub.Infrastructure.Graph;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class RemoteWorkCalendarTests
{
    [Fact]
    public void Built_event_is_all_day_working_elsewhere_in_paris_time()
    {
        var day = new DateOnly(2026, 6, 22);

        var evt = RemoteWorkEventFactory.BuildRemoteWorkEvent(day);

        Assert.True(evt.IsAllDay);
        Assert.Equal(FreeBusyStatus.WorkingElsewhere, evt.ShowAs);
        Assert.Equal("Europe/Paris", evt.Start!.TimeZone);
        Assert.Equal("Europe/Paris", evt.End!.TimeZone);
    }

    [Fact]
    public void Built_event_spans_midnight_to_next_midnight()
    {
        var day = new DateOnly(2026, 6, 22);

        var evt = RemoteWorkEventFactory.BuildRemoteWorkEvent(day);

        Assert.Equal("2026-06-22T00:00:00", evt.Start!.DateTime);
        Assert.Equal("2026-06-23T00:00:00", evt.End!.DateTime);
    }

    [Fact]
    public void Month_window_is_first_of_month_to_first_of_next_month()
    {
        var (start, end) = RemoteWorkCalendarQuery.MonthWindow(2026, 6);

        Assert.Equal("2026-06-01T00:00:00", start);
        Assert.Equal("2026-07-01T00:00:00", end);
    }

    [Fact]
    public void Month_window_rolls_over_year_boundary_in_december()
    {
        var (start, end) = RemoteWorkCalendarQuery.MonthWindow(2026, 12);

        Assert.Equal("2026-12-01T00:00:00", start);
        Assert.Equal("2027-01-01T00:00:00", end);
    }

    [Fact]
    public void Extracts_only_remote_work_event_dates_sorted_and_distinct()
    {
        var events = new[]
        {
            RemoteWorkEventFactory.BuildRemoteWorkEvent(new DateOnly(2026, 6, 9)),
            RemoteWorkEventFactory.BuildRemoteWorkEvent(new DateOnly(2026, 6, 2)),
            RemoteWorkEventFactory.BuildRemoteWorkEvent(new DateOnly(2026, 6, 9)), // duplicate
            new Event { IsAllDay = true, ShowAs = FreeBusyStatus.Busy, Start = new DateTimeTimeZone { DateTime = "2026-06-15T09:00:00", TimeZone = "Europe/Paris" } },
            new Event { IsAllDay = false, ShowAs = FreeBusyStatus.WorkingElsewhere, Start = new DateTimeTimeZone { DateTime = "2026-06-16T00:00:00", TimeZone = "Europe/Paris" } },
        };

        var dates = RemoteWorkCalendarQuery.ExtractRemoteWorkDates(events);

        Assert.Equal([new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 9)], dates);
    }

    [Fact]
    public void Scopes_are_the_user_consentable_delegated_permissions()
    {
        // ChannelMessage.Read.All is intentionally excluded: it requires admin consent and,
        // bundled into the single interactive request, would block the whole sign-in.
        Assert.Equal(
            ["Mail.Read", "Calendars.ReadWrite", "Chat.Read", "User.Read"],
            GraphScopes.Delegated);
        Assert.DoesNotContain("ChannelMessage.Read.All", GraphScopes.Delegated);
    }

    [Fact]
    public void Important_messages_filter_matches_unread_or_flagged()
    {
        Assert.Equal("isRead eq false or flag/flagStatus eq 'flagged'", GraphEmailQuery.ImportantMessagesFilter);
    }
}
