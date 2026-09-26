namespace MyWorkHub.Core.Features.Automations;

/// <summary>How an automation run ended: its overall status, a one-line error summary and every step's result.</summary>
/// <param name="Status">Overall status (see <see cref="FromSteps"/> for how it is derived).</param>
/// <param name="ErrorMessage">Summary of the failed steps; null when none failed.</param>
/// <param name="Steps">Per-step results, in execution order.</param>
public sealed record AutomationRunOutcome(
    AutomationRunStatus Status,
    string? ErrorMessage,
    IReadOnlyList<AutomationStepResult> Steps)
{
    private const string CANCELLED_MESSAGE = "The run was cancelled.";

    /// <summary>
    /// Derives the overall status from the steps: <see cref="AutomationRunStatus.FAILED"/> when steps
    /// failed and none succeeded, <see cref="AutomationRunStatus.PARTIAL"/> when some failed and
    /// some succeeded, <see cref="AutomationRunStatus.SUCCESS"/> otherwise (including a run whose steps
    /// were all skipped because there was nothing to do).
    /// </summary>
    public static AutomationRunOutcome FromSteps(IReadOnlyList<AutomationStepResult> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var failed = steps.Where(s => s.Status == AutomationStepStatus.FAILED).ToList();
        var anySucceeded = steps.Any(s => s.Status == AutomationStepStatus.SUCCESS);

        AutomationRunStatus status;
        if (failed.Count == 0)
        {
            status = AutomationRunStatus.SUCCESS;
        }
        else
        {
            status = anySucceeded ? AutomationRunStatus.PARTIAL : AutomationRunStatus.FAILED;
        }

        var errorMessage = failed.Count == 0 ? null : string.Join("; ", failed.Select(s => $"{s.Name}: {s.Message}"));
        return new AutomationRunOutcome(status, errorMessage, steps);
    }

    /// <summary>A run stopped before completion; the steps that did finish are kept.</summary>
    public static AutomationRunOutcome Cancelled(IReadOnlyList<AutomationStepResult> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return new AutomationRunOutcome(AutomationRunStatus.CANCELLED, CANCELLED_MESSAGE, steps);
    }
}
