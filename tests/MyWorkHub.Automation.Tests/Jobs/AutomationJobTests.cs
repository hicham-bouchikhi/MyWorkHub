using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Automation.Jobs;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Automation.Tests.Jobs;

public sealed class AutomationJobTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_close_the_run_record_as_failed_when_the_job_throws_outside_a_step()
    {
        var runLog = new FakeAutomationLogger();

        var outcome = await new BrokenJob(runLog).RunAsync(Ct);

        Assert.Equal(AutomationRunStatus.FAILED, outcome.Status);
        Assert.Equal("bug in the job", outcome.ErrorMessage);
        Assert.Equal("first", Assert.Single(runLog.LastOutcome.Steps).Name);
    }

    [Fact]
    public async Task Should_not_run_a_step_when_the_run_is_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var recorder = new AutomationStepRecorder(NullLogger.Instance);
        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recorder.RunAsync("step", _ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, cts.Token));

        Assert.False(ran);
        Assert.Empty(recorder.Results);
    }

    /// <summary>Records one step, then throws from the job body itself.</summary>
    private sealed class BrokenJob(IAutomationLogger runLog)
        : AutomationJob(AutomationCatalog.RemoteWorkSync, runLog, NullLogger.Instance, notifications: null)
    {
        protected override async Task RunStepsAsync(AutomationStepRecorder steps, CancellationToken ct)
        {
            await steps.RunAsync("first", _ => Task.CompletedTask, ct);
            throw new InvalidOperationException("bug in the job");
        }
    }
}
