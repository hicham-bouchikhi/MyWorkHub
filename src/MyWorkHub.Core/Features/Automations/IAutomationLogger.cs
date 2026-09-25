namespace MyWorkHub.Core.Features.Automations;

/// <summary>One recorded execution of an automation, as shown in the run history.</summary>
/// <param name="Id">Run id.</param>
/// <param name="AutomationId">Which automation ran (an <see cref="AutomationCatalog"/> id).</param>
/// <param name="StartedAt">When it started (UTC).</param>
/// <param name="CompletedAt">When it finished (UTC); null while running.</param>
/// <param name="Status">Overall status.</param>
/// <param name="ErrorMessage">Summary of what failed, if anything.</param>
/// <param name="Steps">Per-step results, in execution order (empty while running).</param>
public sealed record AutomationRunRecord(
    Guid Id,
    string AutomationId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    AutomationRunStatus Status,
    string? ErrorMessage,
    IReadOnlyList<AutomationStepResult> Steps);

/// <summary>Records automation runs (start, per-step outcome, end) and exposes the recent history.</summary>
public interface IAutomationLogger
{
    /// <summary>Opens a <see cref="AutomationRunStatus.PENDING"/> run record and returns its id.</summary>
    Task<Guid> BeginRunAsync(string automationId, CancellationToken ct = default);

    /// <summary>Closes the run with its outcome; an unknown id is ignored.</summary>
    Task CompleteRunAsync(Guid runId, AutomationRunOutcome outcome, CancellationToken ct = default);

    /// <summary>The <paramref name="count"/> most recent runs of every automation, newest first.</summary>
    Task<IReadOnlyList<AutomationRunRecord>> GetRecentRunsAsync(int count, CancellationToken ct = default);
}
