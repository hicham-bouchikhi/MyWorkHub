using MyWorkHub.Core.Abstractions;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class NotificationCenterViewModelTests
{
    [Fact]
    public void Add_inserts_newest_first_and_increments_unread()
    {
        var vm = new NotificationCenterViewModel();

        vm.Add("Review", "PR #1 ready", NotificationSeverity.SUCCESS);
        vm.Add("Review", "PR #2 failed", NotificationSeverity.ERROR);

        Assert.Equal(2, vm.UnreadCount);
        Assert.True(vm.HasUnread);
        Assert.True(vm.HasAny);
        Assert.Equal("PR #2 failed", vm.Notifications[0].Message); // newest first
        Assert.Equal("PR #1 ready", vm.Notifications[1].Message);
    }

    [Fact]
    public void Add_trims_history_to_the_cap()
    {
        var vm = new NotificationCenterViewModel();

        for (var i = 0; i < 250; i++)
        {
            vm.Add("Review", $"msg {i}", NotificationSeverity.INFORMATION);
        }

        Assert.Equal(200, vm.Notifications.Count);
        Assert.Equal("msg 249", vm.Notifications[0].Message);   // newest kept
        Assert.Equal("msg 50", vm.Notifications[^1].Message);   // oldest 50 dropped
    }

    [Fact]
    public void Mark_all_read_clears_the_unread_count_and_flags_entries()
    {
        var vm = new NotificationCenterViewModel();
        vm.Add("Review", "a", NotificationSeverity.INFORMATION);
        vm.Add("Review", "b", NotificationSeverity.WARNING);

        vm.MarkAllReadCommand.Execute(null);

        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.HasUnread);
        Assert.All(vm.Notifications, e => Assert.True(e.IsRead));
    }

    [Fact]
    public void Clear_empties_the_history_and_unread_count()
    {
        var vm = new NotificationCenterViewModel();
        vm.Add("Review", "a", NotificationSeverity.INFORMATION);

        vm.ClearCommand.Execute(null);

        Assert.Empty(vm.Notifications);
        Assert.Equal(0, vm.UnreadCount);
        Assert.False(vm.HasAny);
    }

    [Fact]
    public void Begin_and_end_activity_track_in_progress_operations()
    {
        var vm = new NotificationCenterViewModel();

        Assert.False(vm.HasActive);

        var first = vm.BeginActivity("Repo PR #42", () => { });
        var second = vm.BeginActivity("Repo PR #7", () => { });
        Assert.True(vm.HasActive);
        Assert.Equal(2, vm.ActiveOperations.Count);

        vm.EndActivity(first);
        Assert.True(vm.HasActive);
        Assert.Equal(["Repo PR #7"], vm.ActiveOperations.Select(o => o.Label));

        vm.EndActivity(second);
        Assert.False(vm.HasActive);
        Assert.Empty(vm.ActiveOperations);
    }

    [Fact]
    public void End_activity_is_safe_for_an_operation_already_removed()
    {
        var vm = new NotificationCenterViewModel();
        var op = vm.BeginActivity("Repo PR #1", () => { });

        vm.EndActivity(op);
        vm.EndActivity(op); // second call must not throw

        Assert.False(vm.HasActive);
    }

    [Fact]
    public void Cancelling_an_active_operation_runs_its_callback_and_marks_it_cancelling()
    {
        var vm = new NotificationCenterViewModel();
        var cancelled = 0;
        var op = vm.BeginActivity("Summarising \"Hello\"", () => cancelled++);

        Assert.False(op.IsCancelling);

        op.CancelCommand.Execute(null);

        Assert.Equal(1, cancelled);
        Assert.True(op.IsCancelling);
    }

    [Fact]
    public void Cancelling_twice_only_runs_the_callback_once()
    {
        var vm = new NotificationCenterViewModel();
        var cancelled = 0;
        var op = vm.BeginActivity("Repo PR #9", () => cancelled++);

        op.CancelCommand.Execute(null);
        op.CancelCommand.Execute(null);

        Assert.Equal(1, cancelled);
    }

    [Fact]
    public void Activating_an_entry_runs_its_action_and_marks_it_read()
    {
        var vm = new NotificationCenterViewModel();
        var activated = false;
        vm.Add("Email summary", "ready", NotificationSeverity.SUCCESS, onActivated: () => activated = true);
        var entry = vm.Notifications[0];

        Assert.True(entry.IsActionable);

        entry.ActivateCommand.Execute(null);

        Assert.True(activated);
        Assert.True(entry.IsRead);
    }

    [Fact]
    public void An_entry_without_an_action_is_not_actionable()
    {
        var vm = new NotificationCenterViewModel();
        vm.Add("Review", "done", NotificationSeverity.INFORMATION);

        Assert.False(vm.Notifications[0].IsActionable);
    }

    [Fact]
    public void Unread_badge_caps_at_9_plus()
    {
        var vm = new NotificationCenterViewModel();

        for (var i = 0; i < 12; i++)
        {
            vm.Add("Review", $"msg {i}", NotificationSeverity.INFORMATION);
        }

        Assert.Equal("9+", vm.UnreadBadge);
    }
}
