namespace MyWorkHub.Core.Entities;

/// <summary>Lifecycle status of a single automation run.</summary>
public enum AutomationStatus
{
    PENDING,
    SUCCESS,
    FAILED,
    CANCELLED,
}
