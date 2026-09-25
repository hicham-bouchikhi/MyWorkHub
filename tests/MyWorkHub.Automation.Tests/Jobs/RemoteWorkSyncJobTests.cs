using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Automation.Jobs;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Automation.Tests.Jobs;

/// <summary>
/// The hard requirement of this job: Outlook, PeopleNet and mwork are synced independently — one target
/// failing must never stop the other two, and the run history records each target's own outcome.
/// </summary>
public sealed class RemoteWorkSyncJobTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly FakePlanRepository _plans = new();
    private readonly FakeRemoteWorkSync _sync = new();
    private readonly FakeAutomationLogger _runLog = new();
    private readonly RecordingNotificationService _notifications = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RemoteWorkSyncJob Job()
        => new(_plans, _sync, _runLog, new FixedTimeProvider(_now), NullLogger<RemoteWorkSyncJob>.Instance, _notifications);

    private static AutomationStepResult Step(AutomationRunOutcome outcome, string name)
        => Assert.Single(outcome.Steps, s => s.Name == name);

    public static TheoryData<string, string> EachTarget => new()
    {
        { FakeRemoteWorkSync.OUTLOOK, RemoteWorkSyncJob.OUTLOOK_STEP },
        { FakeRemoteWorkSync.PEOPLENET, RemoteWorkSyncJob.PEOPLENET_STEP },
        { FakeRemoteWorkSync.MWORK, RemoteWorkSyncJob.MWORK_STEP },
    };

    [Theory]
    [MemberData(nameof(EachTarget))]
    public async Task Should_still_sync_the_other_two_targets_when_one_target_throws(string failingTarget, string failingStep)
    {
        _sync.Failures[failingTarget] = new InvalidOperationException("site is down");

        var outcome = await Job().RunAsync(Ct);

        // All three were attempted, in order, whichever one failed.
        Assert.Equal([FakeRemoteWorkSync.OUTLOOK, FakeRemoteWorkSync.PEOPLENET, FakeRemoteWorkSync.MWORK], _sync.Called);

        // Per-step outcome, not a single boolean: only the failing step is FAILED, with its own error.
        Assert.Equal(3, outcome.Steps.Count);
        var failed = Step(outcome, failingStep);
        Assert.Equal(AutomationStepStatus.FAILED, failed.Status);
        Assert.Equal("site is down", failed.Message);
        Assert.All(outcome.Steps.Where(s => s.Name != failingStep), s => Assert.Equal(AutomationStepStatus.SUCCESS, s.Status));

        Assert.Equal(AutomationRunStatus.PARTIAL, outcome.Status);
        Assert.Equal($"{failingStep}: site is down", outcome.ErrorMessage);
    }

    [Fact]
    public async Task Should_attempt_every_target_and_record_each_error_when_all_three_throw()
    {
        _sync.Failures[FakeRemoteWorkSync.OUTLOOK] = new InvalidOperationException("not signed in");
        _sync.Failures[FakeRemoteWorkSync.PEOPLENET] = new TimeoutException("login timed out");
        _sync.Failures[FakeRemoteWorkSync.MWORK] = new HttpRequestException("502");

        var outcome = await Job().RunAsync(Ct);

        Assert.Equal(3, _sync.Called.Count);
        Assert.Equal(AutomationRunStatus.FAILED, outcome.Status);
        Assert.Equal("not signed in", Step(outcome, RemoteWorkSyncJob.OUTLOOK_STEP).Message);
        Assert.Equal("login timed out", Step(outcome, RemoteWorkSyncJob.PEOPLENET_STEP).Message);
        Assert.Equal("502", Step(outcome, RemoteWorkSyncJob.MWORK_STEP).Message);
    }

    [Fact]
    public async Task Should_persist_the_per_step_outcome_in_the_run_history()
    {
        _sync.Failures[FakeRemoteWorkSync.PEOPLENET] = new InvalidOperationException("login rejected");

        var outcome = await Job().RunAsync(Ct);

        Assert.Equal([AutomationCatalog.RemoteWorkSync.Id], _runLog.Begun);
        Assert.Same(outcome, _runLog.LastOutcome);
        Assert.Equal(
            [AutomationStepStatus.SUCCESS, AutomationStepStatus.FAILED, AutomationStepStatus.SUCCESS],
            _runLog.LastOutcome.Steps.Select(s => s.Status));
    }

    [Fact]
    public async Task Should_succeed_when_every_target_synced()
    {
        var outcome = await Job().RunAsync(Ct);

        Assert.Equal(AutomationRunStatus.SUCCESS, outcome.Status);
        Assert.Equal(
            [RemoteWorkSyncJob.OUTLOOK_STEP, RemoteWorkSyncJob.PEOPLENET_STEP, RemoteWorkSyncJob.MWORK_STEP],
            outcome.Steps.Select(s => s.Name));
        Assert.Equal(NotificationSeverity.SUCCESS, Assert.Single(_notifications.Notifications).Severity);
    }

    [Fact]
    public async Task Should_sync_the_plan_of_the_current_month()
    {
        await Job().RunAsync(Ct);

        Assert.Equal((2026, 9), Assert.Single(_plans.Requests));
    }

    [Fact]
    public async Task Should_skip_every_target_when_no_day_is_planned()
    {
        _plans.Plan = RemoteWorkPlan.Create(2026, 9, []);

        var outcome = await Job().RunAsync(Ct);

        Assert.Empty(_sync.Called);
        Assert.All(outcome.Steps, s => Assert.Equal(AutomationStepStatus.SKIPPED, s.Status));
        Assert.Contains("September 2026", outcome.Steps[0].Message, StringComparison.Ordinal);
        Assert.Equal(AutomationRunStatus.SUCCESS, outcome.Status);
    }

    [Fact]
    public async Task Should_fail_every_target_without_calling_any_when_the_plan_cannot_be_loaded()
    {
        _plans.Failure = new InvalidOperationException("database locked");

        var outcome = await Job().RunAsync(Ct);

        Assert.Empty(_sync.Called);
        Assert.Equal(3, outcome.Steps.Count);
        Assert.All(outcome.Steps, s =>
        {
            Assert.Equal(AutomationStepStatus.FAILED, s.Status);
            Assert.Contains("database locked", s.Message, StringComparison.Ordinal);
        });
        Assert.Equal(AutomationRunStatus.FAILED, outcome.Status);
    }

    [Fact]
    public async Task Should_treat_a_timeout_inside_one_target_as_that_target_failing()
    {
        // A TaskCanceledException from e.g. an HTTP timeout is not the run being cancelled.
        _sync.Failures[FakeRemoteWorkSync.OUTLOOK] = new TaskCanceledException("request timed out");

        var outcome = await Job().RunAsync(Ct);

        Assert.Equal(3, _sync.Called.Count);
        Assert.Equal(AutomationStepStatus.FAILED, Step(outcome, RemoteWorkSyncJob.OUTLOOK_STEP).Status);
        Assert.Equal(AutomationRunStatus.PARTIAL, outcome.Status);
    }

    [Fact]
    public async Task Should_stop_and_still_record_the_run_as_cancelled_when_the_run_is_cancelled()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        _sync.OnCalled = target =>
        {
            if (target == FakeRemoteWorkSync.PEOPLENET)
            {
                cts.Cancel();
            }
        };
        _sync.Failures[FakeRemoteWorkSync.PEOPLENET] = new OperationCanceledException(cts.Token);

        var outcome = await Job().RunAsync(cts.Token);

        Assert.Equal([FakeRemoteWorkSync.OUTLOOK, FakeRemoteWorkSync.PEOPLENET], _sync.Called);
        Assert.Equal(AutomationRunStatus.CANCELLED, outcome.Status);
        Assert.Equal(RemoteWorkSyncJob.OUTLOOK_STEP, Assert.Single(outcome.Steps).Name);
        Assert.Same(outcome, _runLog.LastOutcome);
    }

    [Fact]
    public async Task Should_warn_the_user_when_the_run_partly_failed()
    {
        _sync.Failures[FakeRemoteWorkSync.MWORK] = new InvalidOperationException("layout changed");

        await Job().RunAsync(Ct);

        var notification = Assert.Single(_notifications.Notifications);
        Assert.Equal(NotificationSeverity.WARNING, notification.Severity);
        Assert.Equal(AutomationCatalog.RemoteWorkSync.DisplayName, notification.Title);
        Assert.Contains("layout changed", notification.Message, StringComparison.Ordinal);
    }
}
