using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Core.Modules;
using Quartz;

namespace MyWorkHub.Automation.Scheduling;

public static class AutomationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Quartz scheduler (with the Microsoft DI job factory, so jobs resolve their Core
    /// dependencies from the container) and every discovered <see cref="IAutomationModule"/>: its job is
    /// always added (durable, for "Run now"), and a cron trigger in the local time zone is added when the
    /// automation is enabled with a valid expression. Also exposes the scheduler to the UI as
    /// <see cref="IAutomationScheduler"/>. The scheduler is started by the host.
    /// </summary>
    /// <param name="services">The container being composed.</param>
    /// <param name="configuration">App configuration (the <c>Automation</c> section holds the schedules).</param>
    /// <param name="moduleAssemblies">Assemblies to scan for modules; defaults to this (Automation) assembly.</param>
    [RequiresUnreferencedCode("Discovers automation modules via reflection.")]
    public static IServiceCollection AddAutomations(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assemblies = moduleAssemblies ?? [typeof(AutomationServiceCollectionExtensions).Assembly];
        IReadOnlyList<AutomationRegistration> registrations = ModuleDiscovery.Find<IAutomationModule>(assemblies)
            .Select(m => AutomationRegistration.FromConfiguration(m, configuration))
            .OrderBy(r => r.Automation.DisplayName, StringComparer.Ordinal)
            .ToList();

        services.AddQuartz(quartz =>
        {
            foreach (var registration in registrations)
            {
                quartz.AddJob(
                    registration.JobType,
                    registration.JobKey,
                    job => job.StoreDurably().WithDescription(registration.Automation.DisplayName));

                if (registration.CronExpression is { } cron)
                {
                    quartz.AddTrigger(trigger => trigger
                        .WithIdentity(registration.ScheduleTriggerKey)
                        .ForJob(registration.JobKey)
                        .WithCronSchedule(cron, schedule => schedule.InTimeZone(TimeZoneInfo.Local)));
                }
            }
        });

        foreach (var registration in registrations)
        {
            services.AddTransient(registration.JobType);
        }

        services.AddSingleton(registrations);
        services.AddSingleton<IAutomationScheduler, QuartzAutomationScheduler>();

        return services;
    }
}
