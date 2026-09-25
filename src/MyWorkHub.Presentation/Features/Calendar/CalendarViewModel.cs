using System.Collections.ObjectModel;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Presentation.Features.GraphAuth;

namespace MyWorkHub.Presentation.Features.Calendar;

/// <summary>The coming week's Outlook calendar, read-only. Not a deep-link target.</summary>
public sealed class CalendarViewModel : GraphPageViewModel
{
    private const int DAYS_AHEAD = 7;

    private readonly ICalendarService? _calendar;

    public CalendarViewModel(ICalendarService? calendar = null, IGraphConnectionService? connection = null)
        : base("Calendar", calendar is not null, connection)
    {
        _calendar = calendar;
    }

    /// <summary>Which window the page shows.</summary>
    public string Subtitle { get; } = $"Next {DAYS_AHEAD} days";

    /// <summary>Upcoming events in start order.</summary>
    public ObservableCollection<CalendarEventRowViewModel> Items { get; } = [];

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        var events = await _calendar!.GetUpcomingEventsAsync(DAYS_AHEAD, ct);

        Items.Clear();
        foreach (var calendarEvent in events)
        {
            Items.Add(new CalendarEventRowViewModel(calendarEvent));
        }
    }
}
