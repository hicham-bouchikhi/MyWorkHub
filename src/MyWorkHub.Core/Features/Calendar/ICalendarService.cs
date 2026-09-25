using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Core.Features.Calendar;

/// <summary>
/// Read access to the user's Outlook calendar. Throws <see cref="GraphNotConnectedException"/> when
/// the user is not signed in to Microsoft 365.
/// </summary>
public interface ICalendarService
{
    /// <summary>
    /// Non-cancelled events overlapping the next <paramref name="days"/> days (starting now),
    /// ordered by start time. Recurring series are expanded into their occurrences.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(int days, CancellationToken ct = default);
}
