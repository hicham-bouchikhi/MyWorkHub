using System.Globalization;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations.RemoteWork;

/// <summary>
/// Writes remote-work days to the Outlook calendar through Microsoft Graph: one all-day,
/// "working elsewhere", reminder-less event per planned day. Idempotent — days that already have such an
/// event (same subject, all-day) are skipped, and events are never deleted, so days the user removed from
/// the plan (or added by hand) are left alone.
/// <para>
/// This is the automation slice's own write path rather than part of <c>ICalendarService</c>, which is the
/// Calendar page's read-only "upcoming events" view: extending that contract with writes would change
/// another slice for a need only this job has.
/// </para>
/// </summary>
internal sealed class OutlookRemoteWorkCalendar
{
    /// <summary>Subject of the events this automation creates (and recognises as its own).</summary>
    public const string EVENT_SUBJECT = "Remote work";

    private const string PREFER_HEADER = "Prefer";
    private const string DATE_TIME_FORMAT = "yyyy-MM-dd'T'HH:mm:ss";
    private const int MAX_EVENTS = 500;

    private static readonly string[] _eventFields = ["subject", "isAllDay", "start"];

    private readonly GraphServiceClient _graph;
    private readonly TimeProvider _timeProvider;

    public OutlookRemoteWorkCalendar(GraphServiceClient graph, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _graph = graph;
        _timeProvider = timeProvider;
    }

    /// <summary>Creates the missing events and returns how many were created.</summary>
    public async Task<int> AddMissingDaysAsync(RemoteWorkPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Days.Count == 0)
        {
            return 0;
        }

        // All-day events are wall-clock dates: read and write them in the user's own time zone so a
        // "2026-09-25" event is that day in Outlook, not the day either side of it.
        var timeZone = GraphTimeZoneId(_timeProvider.LocalTimeZone);
        var existing = await GetExistingDaysAsync(plan, timeZone, ct).ConfigureAwait(false);

        var created = 0;
        foreach (var day in plan.Days.Where(d => !existing.Contains(d)))
        {
            await _graph.Me.Events.PostAsync(NewEvent(day, timeZone), cancellationToken: ct).ConfigureAwait(false);
            created++;
        }

        return created;
    }

    private async Task<HashSet<DateOnly>> GetExistingDaysAsync(RemoteWorkPlan plan, string timeZone, CancellationToken ct)
    {
        var monthStart = new DateOnly(plan.Year, plan.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        var response = await _graph.Me.CalendarView.GetAsync(request =>
        {
            request.QueryParameters.StartDateTime = Format(monthStart);
            request.QueryParameters.EndDateTime = Format(nextMonthStart);
            request.QueryParameters.Top = MAX_EVENTS;
            request.QueryParameters.Select = _eventFields;
            request.Headers.Add(PREFER_HEADER, $"outlook.timezone=\"{timeZone}\"");
        }, ct).ConfigureAwait(false);

        return (response?.Value ?? [])
            .Where(e => e.IsAllDay == true && string.Equals(e.Subject, EVENT_SUBJECT, StringComparison.OrdinalIgnoreCase))
            .Select(e => DateTime.TryParse(e.Start?.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                ? DateOnly.FromDateTime(start)
                : (DateOnly?)null)
            .OfType<DateOnly>()
            .ToHashSet();
    }

    private static Event NewEvent(DateOnly day, string timeZone) => new()
    {
        Subject = EVENT_SUBJECT,
        IsAllDay = true,
        ShowAs = FreeBusyStatus.WorkingElsewhere,
        IsReminderOn = false,
        Start = new DateTimeTimeZone { DateTime = Format(day), TimeZone = timeZone },
        End = new DateTimeTimeZone { DateTime = Format(day.AddDays(1)), TimeZone = timeZone },
    };

    private static string Format(DateOnly day)
        => day.ToDateTime(TimeOnly.MinValue).ToString(DATE_TIME_FORMAT, CultureInfo.InvariantCulture);

    // Graph accepts Windows time-zone names everywhere; on Linux/macOS the local zone has an IANA id.
    internal static string GraphTimeZoneId(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone.HasIanaId && TimeZoneInfo.TryConvertIanaIdToWindowsId(zone.Id, out var windowsId)
            ? windowsId
            : zone.Id;
    }
}
