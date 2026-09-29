using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class NotificationSettingsViewModelTests
{
    private readonly FakeSettingsService _settings = new()
    {
        Notifications = new NotificationSettings(15, Emails: true, TeamsChats: false, PullRequests: true,
            WorkItems: false, Mentions: true, CalendarEvents: false),
    };

    private NotificationSettingsViewModel Loaded()
    {
        var vm = new NotificationSettingsViewModel(_settings);
        vm.Load();
        return vm;
    }

    [Fact]
    public void Should_show_the_persisted_interval_and_switches_without_saving_when_loading()
    {
        var vm = Loaded();

        Assert.Equal(15m, vm.RefreshIntervalMinutes);
        Assert.True(vm.Emails);
        Assert.False(vm.TeamsChats);
        Assert.True(vm.PullRequests);
        Assert.False(vm.WorkItems);
        Assert.True(vm.Mentions);
        Assert.False(vm.CalendarEvents);
        Assert.Empty(_settings.Saves);
    }

    [Fact]
    public async Task Should_persist_at_once_when_a_switch_is_flipped()
    {
        var vm = Loaded();

        vm.TeamsChats = true;
        await vm.LastSave;

        Assert.Equal(_settings.Notifications, Assert.Single(_settings.Saves));
        Assert.True(_settings.Notifications.TeamsChats);
    }

    [Fact]
    public async Task Should_persist_the_interval_within_bounds_when_it_changes()
    {
        var vm = Loaded();

        vm.RefreshIntervalMinutes = 0;
        await vm.LastSave;

        Assert.Equal(1, _settings.Notifications.RefreshIntervalMinutes);
        Assert.Equal(1m, vm.RefreshIntervalMinutes);
    }

    [Fact]
    public async Task Should_report_a_failed_save()
    {
        var vm = Loaded();
        _settings.Failure = new IOException("disk full");

        vm.Emails = false;
        await vm.LastSave;

        Assert.Contains("disk full", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_be_unavailable_without_a_settings_service()
    {
        Assert.False(new NotificationSettingsViewModel().IsAvailable);
    }
}
