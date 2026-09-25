using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Features.Automations;
using Quartz;

namespace MyWorkHub.Automation.Scheduling;

/// <summary>
/// One discovered automation as registered with Quartz: its job, and its schedule resolved from
/// configuration section <c>Automation:&lt;Id&gt;</c> (keys <c>Enabled</c>, <c>CronExpression</c>, read key by
/// key rather than through the reflection-based binder). A job is always registered (durable), so
/// "Run now" works even when it has no schedule.
/// </summary>
/// <param name="Automation">Which automation.</param>
/// <param name="JobType">The Quartz job type.</param>
/// <param name="CronExpression">The validated cron expression it is scheduled on; null when not scheduled.</param>
/// <param name="Note">Why it is not scheduled; null when it is.</param>
internal sealed record AutomationRegistration(
    AutomationDescriptor Automation,
    Type JobType,
    string? CronExpression,
    string? Note)
{
    public const string SECTION = "Automation";

    private const string SCHEDULE_TRIGGER_SUFFIX = ".schedule";

    public JobKey JobKey => new(Automation.Id);

    public TriggerKey ScheduleTriggerKey => new(Automation.Id + SCHEDULE_TRIGGER_SUFFIX);

    public static AutomationRegistration FromConfiguration(IAutomationModule module, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(configuration);

        var path = $"{SECTION}:{module.Automation.Id}";
        var section = configuration.GetSection(path);
        var enabled = bool.TryParse(section["Enabled"], out var parsed) && parsed;
        var cron = section["CronExpression"]?.Trim() ?? "";

        if (!enabled)
        {
            return new(module.Automation, module.JobType, null, $"Not scheduled — set {path}:Enabled to true to run it automatically.");
        }

        if (!Quartz.CronExpression.IsValidExpression(cron))
        {
            return new(module.Automation, module.JobType, null, $"Not scheduled — {path}:CronExpression \"{cron}\" is not a valid Quartz cron expression.");
        }

        return new(module.Automation, module.JobType, cron, null);
    }
}
