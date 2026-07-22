using System.Globalization;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Pure helpers for reading back existing remote-work days from the calendar (T016):
/// the calendar-view window for a month, and the predicate that recognises a
/// remote-work event.
/// </summary>
public static class RemoteWorkCalendarQuery
{
    /// <summary>Inclusive start / exclusive end of the given month, formatted for Graph calendarView.</summary>
    public static (string Start, string End) MonthWindow(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        return (FormatMidnight(start), FormatMidnight(end));
    }

    /// <summary>True when the event is one of our all-day "Working Elsewhere" markers.</summary>
    public static bool IsRemoteWorkEvent(Event candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.IsAllDay == true && candidate.ShowAs == FreeBusyStatus.WorkingElsewhere;
    }

    /// <summary>Extracts the distinct start dates of the remote-work events in the set.</summary>
    public static IReadOnlyList<DateOnly> ExtractRemoteWorkDates(IEnumerable<Event> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events
            .Where(IsRemoteWorkEvent)
            .Select(e => e.Start?.DateTime)
            .Where(static dt => dt is not null)
            .Select(dt => DateOnly.FromDateTime(DateTime.Parse(dt!, CultureInfo.InvariantCulture)))
            .Distinct()
            .OrderBy(static d => d)
            .ToList();
    }

    private static string FormatMidnight(DateOnly day)
        // DateOnly.ToString rejects time specifiers, so format the date and append midnight.
        => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00";
}
