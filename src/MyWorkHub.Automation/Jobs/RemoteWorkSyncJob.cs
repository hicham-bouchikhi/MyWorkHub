using System.Globalization;
using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Jobs;

/// <summary>
/// Copies the current month's remote-work plan to Outlook, PeopleNet and mwork.
/// <para>
/// The three sync steps are independent: each runs in its own isolated step (its own <c>try/catch</c>
/// inside <see cref="AutomationStepRecorder"/>), so Outlook being unreachable, a PeopleNet login failure or
/// an mwork layout change only fails <em>that</em> step — the other two still run, and the run history
/// shows the outcome of each one (a mixed result is <see cref="AutomationRunStatus.PARTIAL"/>).
/// </para>
/// </summary>
[DisallowConcurrentExecution]
public sealed class RemoteWorkSyncJob : AutomationJob
{
    internal const string OUTLOOK_STEP = "Outlook calendar";
    internal const string PEOPLENET_STEP = "PeopleNet";
    internal const string MWORK_STEP = "mwork";

    private static readonly string[] _syncSteps = [OUTLOOK_STEP, PEOPLENET_STEP, MWORK_STEP];

    private readonly IRemoteWorkPlanRepository _plans;
    private readonly IRemoteWorkSyncAutomation _sync;
    private readonly TimeProvider _timeProvider;

    public RemoteWorkSyncJob(
        IRemoteWorkPlanRepository plans,
        IRemoteWorkSyncAutomation sync,
        IAutomationLogger runLog,
        TimeProvider timeProvider,
        ILogger<RemoteWorkSyncJob> logger,
        INotificationService? notifications = null)
        : base(AutomationCatalog.RemoteWorkSync, runLog, logger, notifications)
    {
        ArgumentNullException.ThrowIfNull(plans);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _plans = plans;
        _sync = sync;
        _timeProvider = timeProvider;
    }

    protected override async Task RunStepsAsync(AutomationStepRecorder steps, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var today = _timeProvider.GetLocalNow();
        RemoteWorkPlan plan;
        try
        {
            plan = await _plans.GetAsync(today.Year, today.Month, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Without the plan nothing can be synced: every target is marked failed (not skipped), so the
            // run does not look like a success.
            foreach (var step in _syncSteps)
            {
                steps.Fail(step, $"Could not load the remote-work plan: {ex.Message}");
            }

            return;
        }

        if (plan.Days.Count == 0)
        {
            var month = today.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            foreach (var step in _syncSteps)
            {
                steps.Skip(step, $"No remote-work days planned for {month}.");
            }

            return;
        }

        // Three separate, isolated steps — never one try/catch around all three.
        await steps.RunAsync(OUTLOOK_STEP, c => _sync.SyncOutlookAsync(plan, c), ct).ConfigureAwait(false);
        await steps.RunAsync(PEOPLENET_STEP, c => _sync.SyncPeopleNetAsync(plan, c), ct).ConfigureAwait(false);
        await steps.RunAsync(MWORK_STEP, c => _sync.SyncMWorkAsync(plan, c), ct).ConfigureAwait(false);
    }
}
