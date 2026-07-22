namespace MyWorkHub.Core.Models;

/// <summary>An Azure DevOps work item assigned to the current user.</summary>
public record WorkItem(
    int Id,
    string Title,
    string Type,
    string State,
    string Priority,
    DateTime? DueDate,
    string Url,
    string Effort = "",
    string Project = "");
