using System.Globalization;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Builds the all-day "Working Elsewhere" calendar events used to mark remote-work
/// days (ARCHITECTURE.md §6, T015). All events are anchored to the Europe/Paris zone.
/// </summary>
public static class RemoteWorkEventFactory
{
    public static string TimeZoneId { get; } = "Europe/Paris";

    public static string Subject { get; } = "Télétravail";

    /// <summary>Graph all-day events span midnight-to-midnight with <c>IsAllDay = true</c>.</summary>
    public static Event BuildRemoteWorkEvent(DateOnly day)
    {
        return new Event
        {
            Subject = Subject,
            IsAllDay = true,
            ShowAs = FreeBusyStatus.WorkingElsewhere,
            Start = MidnightAt(day),
            End = MidnightAt(day.AddDays(1)),
        };
    }

    private static DateTimeTimeZone MidnightAt(DateOnly day) => new()
    {
        // DateOnly.ToString rejects time specifiers, so format the date and append midnight.
        DateTime = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00",
        TimeZone = TimeZoneId,
    };
}
