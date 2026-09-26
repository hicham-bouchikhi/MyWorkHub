using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Scheduling;

/// <summary><see cref="IAutomationScheduler"/> over the app's single Quartz scheduler.</summary>
internal sealed class QuartzAutomationScheduler : IAutomationScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IReadOnlyList<AutomationRegistration> _registrations;

    public QuartzAutomationScheduler(ISchedulerFactory schedulerFactory, IReadOnlyList<AutomationRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(schedulerFactory);
        ArgumentNullException.ThrowIfNull(registrations);
        _schedulerFactory = schedulerFactory;
        _registrations = registrations;
    }

    public async Task<IReadOnlyList<AutomationSchedule>> GetSchedulesAsync(CancellationToken ct = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);

        var schedules = new List<AutomationSchedule>(_registrations.Count);
        foreach (var registration in _registrations)
        {
            DateTimeOffset? nextRun = null;
            if (registration.CronExpression is not null)
            {
                var trigger = await scheduler.GetTrigger(registration.ScheduleTriggerKey, ct).ConfigureAwait(false);
                nextRun = trigger?.GetNextFireTimeUtc();
            }

            schedules.Add(new AutomationSchedule(registration.Automation, registration.CronExpression, nextRun, registration.Note));
        }

        return schedules;
    }

    public async Task RunNowAsync(string automationId, CancellationToken ct = default)
    {
        var registration = _registrations.FirstOrDefault(r => string.Equals(r.Automation.Id, automationId, StringComparison.Ordinal))
                           ?? throw new ArgumentException($"Unknown automation \"{automationId}\".", nameof(automationId));

        var scheduler = await _schedulerFactory.GetScheduler(ct).ConfigureAwait(false);
        await scheduler.TriggerJob(registration.JobKey, ct).ConfigureAwait(false);
    }
}
