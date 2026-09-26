namespace MyWorkHub.Core.Features.Automations;

/// <summary>
/// Copies a <see cref="RemoteWorkPlan"/> to each system that must know about remote-work days. The three
/// targets are deliberately separate calls: the sync job runs each one in isolation, so one system being
/// down (or its login expired) never stops the other two. Each call adds the planned days that are
/// missing; none of them removes days the user entered by hand.
/// </summary>
public interface IRemoteWorkSyncAutomation
{
    /// <summary>Adds an all-day "working elsewhere" event to the Outlook calendar for each planned day.</summary>
    Task SyncOutlookAsync(RemoteWorkPlan plan, CancellationToken ct = default);

    /// <summary>Declares the planned days in PeopleNet (HR portal).</summary>
    Task SyncPeopleNetAsync(RemoteWorkPlan plan, CancellationToken ct = default);

    /// <summary>Declares the planned days in mwork (presence planning).</summary>
    Task SyncMWorkAsync(RemoteWorkPlan plan, CancellationToken ct = default);
}
