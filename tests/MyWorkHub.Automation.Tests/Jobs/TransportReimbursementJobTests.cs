using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Automation.Jobs;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Automation.Tests.Jobs;

public sealed class TransportReimbursementJobTests
{
    private readonly FakeTransportAutomation _transport = new();
    private readonly FakeAutomationLogger _runLog = new();
    private readonly RecordingNotificationService _notifications = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TransportReimbursementJob Job()
        => new(_transport, _runLog, NullLogger<TransportReimbursementJob>.Instance, _notifications);

    [Fact]
    public async Task Should_download_then_send_the_attestation()
    {
        var outcome = await Job().RunAsync(Ct);

        Assert.Same(_transport.Attestation, Assert.Single(_transport.Sent));
        Assert.Equal(AutomationRunStatus.SUCCESS, outcome.Status);
        Assert.Equal(
            [TransportReimbursementJob.DOWNLOAD_STEP, TransportReimbursementJob.SEND_STEP],
            outcome.Steps.Select(s => s.Name));
        Assert.Equal("Saved to /tmp/attestation.pdf", outcome.Steps[0].Message);
        Assert.Equal([AutomationCatalog.TransportReimbursement.Id], _runLog.Begun);
    }

    [Fact]
    public async Task Should_skip_sending_and_fail_the_run_when_the_download_fails()
    {
        _transport.DownloadFailure = new InvalidOperationException("TCL: no login saved.");

        var outcome = await Job().RunAsync(Ct);

        Assert.Empty(_transport.Sent);
        Assert.Equal(AutomationStepStatus.FAILED, outcome.Steps[0].Status);
        Assert.Equal("TCL: no login saved.", outcome.Steps[0].Message);
        Assert.Equal(AutomationStepStatus.SKIPPED, outcome.Steps[1].Status);
        Assert.Equal(AutomationRunStatus.FAILED, outcome.Status);
        Assert.Equal(NotificationSeverity.ERROR, Assert.Single(_notifications.Notifications).Severity);
    }

    [Fact]
    public async Task Should_record_a_partial_run_when_only_sending_fails()
    {
        _transport.SendFailure = new InvalidOperationException("No billing address configured.");

        var outcome = await Job().RunAsync(Ct);

        Assert.Equal(AutomationRunStatus.PARTIAL, outcome.Status);
        Assert.Equal(AutomationStepStatus.SUCCESS, outcome.Steps[0].Status);
        Assert.Equal(AutomationStepStatus.FAILED, outcome.Steps[1].Status);
        Assert.Same(outcome, _runLog.LastOutcome);
    }
}
