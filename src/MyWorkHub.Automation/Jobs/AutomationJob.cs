using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Jobs;

/// <summary>
/// Shared shape of every automation job: open a run record, run the job's steps through an
/// <see cref="AutomationStepRecorder"/>, close the record with the per-step outcome, and tell the user how
/// it went. Subclasses only implement <see cref="RunStepsAsync"/>.
/// </summary>
public abstract partial class AutomationJob : IJob
{
    private readonly AutomationDescriptor _automation;
    private readonly IAutomationLogger _runLog;
    private readonly ILogger _logger;
    private readonly INotificationService? _notifications;

    protected AutomationJob(
        AutomationDescriptor automation,
        IAutomationLogger runLog,
        ILogger logger,
        INotificationService? notifications)
    {
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(runLog);
        ArgumentNullException.ThrowIfNull(logger);
        _automation = automation;
        _runLog = runLog;
        _logger = logger;
        _notifications = notifications;
    }

    /// <summary>Quartz entry point (scheduled or "run now"); stops when the scheduler shuts down.</summary>
    public Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return RunAsync(context.CancellationToken);
    }

    /// <summary>Runs the automation once and returns the recorded outcome.</summary>
    public async Task<AutomationRunOutcome> RunAsync(CancellationToken ct = default)
    {
        var runId = await _runLog.BeginRunAsync(_automation.Id, ct).ConfigureAwait(false);
        var steps = new AutomationStepRecorder(_logger);

        AutomationRunOutcome outcome;
        try
        {
            await RunStepsAsync(steps, ct).ConfigureAwait(false);
            outcome = AutomationRunOutcome.FromSteps(steps.Results);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            outcome = AutomationRunOutcome.Cancelled(steps.Results);
        }
        catch (Exception ex)
        {
            // A failure outside any step (a bug in the job itself): still close the run record, so the
            // history never shows it as running forever.
            LogRunFailed(ex, _automation.Id);
            outcome = new AutomationRunOutcome(AutomationRunStatus.FAILED, ex.Message, steps.Results);
        }

        // Recorded even when the run was cancelled, hence not with the (already cancelled) run token.
        await _runLog.CompleteRunAsync(runId, outcome, CancellationToken.None).ConfigureAwait(false);
        Notify(outcome);
        return outcome;
    }

    /// <summary>Runs the automation's steps, each through <paramref name="steps"/>.</summary>
    protected abstract Task RunStepsAsync(AutomationStepRecorder steps, CancellationToken ct);

    private void Notify(AutomationRunOutcome outcome)
    {
        var (severity, message) = outcome.Status switch
        {
            AutomationRunStatus.SUCCESS => (NotificationSeverity.SUCCESS, "Completed successfully."),
            AutomationRunStatus.PARTIAL => (NotificationSeverity.WARNING, $"Partly failed — {outcome.ErrorMessage}"),
            AutomationRunStatus.CANCELLED => (NotificationSeverity.INFORMATION, "Cancelled."),
            _ => (NotificationSeverity.ERROR, $"Failed — {outcome.ErrorMessage}"),
        };
        _notifications?.Notify(_automation.DisplayName, message, severity);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Automation '{Automation}' failed outside of a step")]
    private partial void LogRunFailed(Exception exception, string automation);
}
