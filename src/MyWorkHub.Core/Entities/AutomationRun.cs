namespace MyWorkHub.Core.Entities;

/// <summary>History record for one execution of an automation job.</summary>
public class AutomationRun
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public AutomationStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>JSON — step-by-step result details.</summary>
    public string? Details { get; set; }
}
