using MyWorkHub.Automation.Jobs;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Automation.Scheduling;

/// <summary>
/// One automation's scheduling registration: which automation, and the Quartz job that runs it. Implemented
/// once per automation in this project and discovered automatically by
/// <see cref="AutomationServiceCollectionExtensions.AddAutomations"/> (same model as the layers' feature
/// modules), so adding an automation never means editing the scheduler wiring. The schedule itself comes
/// from configuration (<c>Automation:&lt;Id&gt;:Enabled</c> / <c>CronExpression</c>).
/// Implementations must be non-abstract classes with a public parameterless constructor.
/// </summary>
public interface IAutomationModule
{
    AutomationDescriptor Automation { get; }

    /// <summary>The <see cref="AutomationJob"/> subclass Quartz runs.</summary>
    Type JobType { get; }
}

/// <summary>Schedules <see cref="TransportReimbursementJob"/>.</summary>
public sealed class TransportReimbursementAutomationModule : IAutomationModule
{
    public AutomationDescriptor Automation => AutomationCatalog.TransportReimbursement;

    public Type JobType => typeof(TransportReimbursementJob);
}

/// <summary>Schedules <see cref="RemoteWorkSyncJob"/>.</summary>
public sealed class RemoteWorkSyncAutomationModule : IAutomationModule
{
    public AutomationDescriptor Automation => AutomationCatalog.RemoteWorkSync;

    public Type JobType => typeof(RemoteWorkSyncJob);
}
