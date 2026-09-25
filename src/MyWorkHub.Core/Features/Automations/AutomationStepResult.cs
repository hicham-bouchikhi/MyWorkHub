namespace MyWorkHub.Core.Features.Automations;

/// <summary>Outcome of one step of an automation run.</summary>
public enum AutomationStepStatus
{
    SUCCESS,
    FAILED,

    /// <summary>Not attempted — a prerequisite step failed, or there was nothing to do.</summary>
    SKIPPED,
}

/// <summary>What happened to one step of an automation run.</summary>
/// <param name="Name">Human-readable step name (e.g. "Outlook calendar").</param>
/// <param name="Status">Whether the step succeeded, failed or was skipped.</param>
/// <param name="Message">The error of a failed step or the reason for a skipped one; null on success.</param>
public sealed record AutomationStepResult(string Name, AutomationStepStatus Status, string? Message = null);
