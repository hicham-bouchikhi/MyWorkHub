namespace MyWorkHub.Core.Features.Automations;

/// <summary>An automation together with its schedule.</summary>
/// <param name="Automation">Which automation.</param>
/// <param name="CronExpression">The cron expression it runs on; null when it only runs on demand.</param>
/// <param name="NextRunAt">Next scheduled run; null when not scheduled.</param>
/// <param name="Note">Why the automation is not scheduled (disabled, invalid expression…); null when it is.</param>
public sealed record AutomationSchedule(
    AutomationDescriptor Automation,
    string? CronExpression,
    DateTimeOffset? NextRunAt,
    string? Note);

/// <summary>The background scheduler as the UI sees it: what is scheduled when, and "run now".</summary>
public interface IAutomationScheduler
{
    /// <summary>Every registered automation with its schedule, in display order.</summary>
    Task<IReadOnlyList<AutomationSchedule>> GetSchedulesAsync(CancellationToken ct = default);

    /// <summary>
    /// Queues an immediate run outside the schedule and returns without waiting for it; the outcome lands
    /// in the run history. Runs of one automation never overlap — a request made while one is in progress
    /// waits for it. Throws <see cref="ArgumentException"/> for an unknown id.
    /// </summary>
    Task RunNowAsync(string automationId, CancellationToken ct = default);
}
