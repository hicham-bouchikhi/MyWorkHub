namespace MyWorkHub.Core.Features.Automations;

/// <summary>
/// Overall outcome of one automation run. Persisted by name, so the pre-existing values keep the names
/// already used by the <c>AutomationRuns.Status</c> column (<c>PENDING</c>, <c>SUCCESS</c>, <c>FAILED</c>,
/// <c>CANCELLED</c>).
/// </summary>
public enum AutomationRunStatus
{
    /// <summary>Started and not finished yet (or the app stopped before it could record the outcome).</summary>
    PENDING,

    /// <summary>No step failed.</summary>
    SUCCESS,

    /// <summary>At least one step failed and at least one other step succeeded.</summary>
    PARTIAL,

    /// <summary>At least one step failed and none succeeded.</summary>
    FAILED,

    /// <summary>Stopped before completion (app shutdown or cancellation).</summary>
    CANCELLED,
}
