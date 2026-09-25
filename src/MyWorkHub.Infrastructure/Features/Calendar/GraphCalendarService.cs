using System.Globalization;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using MyWorkHub.Core.Features.Calendar;

namespace MyWorkHub.Infrastructure.Features.Calendar;

/// <summary>
/// <see cref="ICalendarService"/> over Microsoft Graph <c>/me/calendarView</c>, which expands recurring
/// series into occurrences. Times are requested in UTC (<c>Prefer: outlook.timezone="UTC"</c>).
/// </summary>
internal sealed class GraphCalendarService : ICalendarService
{
    private const string PREFER_HEADER = "Prefer";
    private const string UTC_PREFERENCE = "outlook.timezone=\"UTC\"";
    private const string ROUND_TRIP_FORMAT = "o";
    private const int MAX_EVENTS = 100;

    private static readonly string[] _eventFields =
        ["id", "subject", "start", "end", "isAllDay", "location", "isCancelled"];

    private readonly GraphServiceClient _graph;
    private readonly TimeProvider _timeProvider;

    public GraphCalendarService(GraphServiceClient graph, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _graph = graph;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetUpcomingEventsAsync(int days, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);

        var start = _timeProvider.GetUtcNow();
        var end = start.AddDays(days);

        var response = await _graph.Me.CalendarView.GetAsync(request =>
        {
            request.QueryParameters.StartDateTime = start.ToString(ROUND_TRIP_FORMAT, CultureInfo.InvariantCulture);
            request.QueryParameters.EndDateTime = end.ToString(ROUND_TRIP_FORMAT, CultureInfo.InvariantCulture);
            request.QueryParameters.Top = MAX_EVENTS;
            request.QueryParameters.Orderby = ["start/dateTime"];
            request.QueryParameters.Select = _eventFields;
            request.Headers.Add(PREFER_HEADER, UTC_PREFERENCE);
        }, ct).ConfigureAwait(false);

        return (response?.Value ?? [])
            .Where(e => e.IsCancelled != true)
            .Select(ToCalendarEvent)
            .OfType<CalendarEvent>()
            .OrderBy(e => e.Start)
            .ToList();
    }

    // Occurrences whose times cannot be read are dropped rather than shown at a wrong time.
    internal static CalendarEvent? ToCalendarEvent(Event graphEvent)
    {
        ArgumentNullException.ThrowIfNull(graphEvent);

        if (ParseUtc(graphEvent.Start) is not { } start || ParseUtc(graphEvent.End) is not { } end)
        {
            return null;
        }

        return new CalendarEvent(
            graphEvent.Id ?? "",
            graphEvent.Subject ?? "",
            start,
            end,
            graphEvent.IsAllDay ?? false,
            graphEvent.Location?.DisplayName ?? "");
    }

    // With the UTC preference, Graph returns local-less wall times such as "2026-09-25T09:00:00.0000000".
    private static DateTime? ParseUtc(DateTimeTimeZone? value)
        => DateTime.TryParse(
            value?.DateTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
}
