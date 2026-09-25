using System.Globalization;
using MyWorkHub.Core.Features.Calendar;

namespace MyWorkHub.Presentation.Features.Calendar;

/// <summary>One upcoming event, formatted for display in the user's local time zone and culture.</summary>
public sealed class CalendarEventRowViewModel
{
    private const string DAY_FORMAT = "ddd d MMM";
    private const string TIME_FORMAT = "HH:mm";

    public CalendarEventRowViewModel(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        Subject = string.IsNullOrWhiteSpace(calendarEvent.Subject) ? "(no title)" : calendarEvent.Subject;
        Location = calendarEvent.Location;

        // All-day events are date-only: converting their midnight boundary to local time could shift the day.
        var culture = CultureInfo.CurrentCulture;
        if (calendarEvent.IsAllDay)
        {
            DayText = calendarEvent.Start.ToString(DAY_FORMAT, culture);
            TimeText = "All day";
        }
        else
        {
            var start = calendarEvent.Start.ToLocalTime();
            DayText = start.ToString(DAY_FORMAT, culture);
            TimeText = $"{start.ToString(TIME_FORMAT, culture)} – {calendarEvent.End.ToLocalTime().ToString(TIME_FORMAT, culture)}";
        }
    }

    public string Subject { get; }

    public string Location { get; }

    public string DayText { get; }

    public string TimeText { get; }
}
