namespace MyWorkHub.Core.Abstractions;

/// <summary>Manages "Working Elsewhere" remote-work calendar entries via Microsoft Graph.</summary>
public interface ICalendarService
{
    /// <summary>Creates an all-day "Working Elsewhere" event for each supplied date.</summary>
    Task CreateRemoteWorkEventsAsync(IEnumerable<DateOnly> dates, CancellationToken ct = default);

    /// <summary>Returns the dates in the given month that already have a remote-work event.</summary>
    Task<IReadOnlyList<DateOnly>> GetExistingRemoteWorkDatesAsync(int year, int month, CancellationToken ct = default);
}
