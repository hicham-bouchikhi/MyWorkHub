using MyWorkHub.Core.Abstractions;
using Microsoft.Graph;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary><see cref="ICalendarService"/> backed by Microsoft Graph (T015, T016).</summary>
public sealed class GraphCalendarService : ICalendarService
{
    private readonly GraphServiceClient _graph;

    public GraphCalendarService(GraphServiceClient graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public async Task CreateRemoteWorkEventsAsync(IEnumerable<DateOnly> dates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dates);

        foreach (var day in dates)
        {
            var remoteWorkEvent = RemoteWorkEventFactory.BuildRemoteWorkEvent(day);
            await _graph.Me.Events.PostAsync(remoteWorkEvent, cancellationToken: ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DateOnly>> GetExistingRemoteWorkDatesAsync(int year, int month, CancellationToken ct = default)
    {
        var (start, end) = RemoteWorkCalendarQuery.MonthWindow(year, month);

        var response = await _graph.Me.CalendarView.GetAsync(request =>
        {
            request.QueryParameters.StartDateTime = start;
            request.QueryParameters.EndDateTime = end;
            request.QueryParameters.Top = 100;
            request.QueryParameters.Select = ["subject", "isAllDay", "showAs", "start", "end"];
        }, ct).ConfigureAwait(false);

        var events = response?.Value ?? [];
        return RemoteWorkCalendarQuery.ExtractRemoteWorkDates(events);
    }
}
