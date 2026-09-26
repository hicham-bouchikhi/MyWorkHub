namespace MyWorkHub.Core.Features.Calendar;

/// <summary>One calendar occurrence.</summary>
/// <param name="Id">Graph event id.</param>
/// <param name="Subject">Event title.</param>
/// <param name="Start">Start (UTC).</param>
/// <param name="End">End (UTC).</param>
/// <param name="IsAllDay">Whether it is an all-day event (times are then midnight boundaries).</param>
/// <param name="Location">Location display name; empty when none.</param>
public sealed record CalendarEvent(
    string Id,
    string Subject,
    DateTime Start,
    DateTime End,
    bool IsAllDay,
    string Location);
