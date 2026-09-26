using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Presentation.Features.Calendar;
using MyWorkHub.UI.Tests.Features.GraphAuth;

namespace MyWorkHub.UI.Tests.Features.Calendar;

public sealed class CalendarViewModelTests
{
    private static readonly DateTime _nine = new(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Should_show_the_coming_week_when_loaded()
    {
        var connection = new FakeGraphConnection();
        var calendar = new FakeCalendarService(connection,
            new CalendarEvent("1", "Standup", _nine, _nine.AddMinutes(15), false, "Room 1"),
            new CalendarEvent("2", "", _nine.Date, _nine.Date.AddDays(1), true, ""));
        var viewModel = new CalendarViewModel(calendar, connection);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(7, calendar.RequestedDays);
        Assert.Equal(["Standup", "(no title)"], viewModel.Items.Select(r => r.Subject));
        Assert.Equal("All day", viewModel.Items[1].TimeText);
        Assert.Equal("Next 7 days", viewModel.Subtitle);
    }

    [Fact]
    public async Task Should_offer_sign_in_when_not_connected()
    {
        var connection = new FakeGraphConnection { IsSignedIn = false };
        var viewModel = new CalendarViewModel(new FakeCalendarService(connection), connection);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsSignIn);
    }

    [Fact]
    public async Task Should_degrade_gracefully_when_no_calendar_service_is_registered()
    {
        var viewModel = new CalendarViewModel();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsAvailable);
        Assert.Empty(viewModel.Items);
    }
}
