using MyWorkHub.Core.Entities;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Records automation job executions and exposes their run history.</summary>
public interface IAutomationLogger
{
    /// <summary>Opens a new run record for the named job and returns its id.</summary>
    Task<Guid> BeginRunAsync(string jobName, CancellationToken ct = default);

    /// <summary>Closes a run record with its outcome.</summary>
    Task CompleteRunAsync(Guid runId, bool success, string? errorMessage = null, CancellationToken ct = default);

    /// <summary>Most recent runs for the named job, newest first.</summary>
    Task<IReadOnlyList<AutomationRun>> GetHistoryAsync(string jobName, int count = 30, CancellationToken ct = default);
}
