namespace MyWorkHub.Core.Entities;

/// <summary>Remote work plan for a given month — source of truth for the sync job.</summary>
public class RemoteWorkSchedule
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>JSON array of day numbers, e.g. [2, 5, 9, 12].</summary>
    public string DaysJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>JSON: { "peoplenet": "ok", "outlook": "ok", "mwork": "failed" }.</summary>
    public string? SyncStatusJson { get; set; }
}
