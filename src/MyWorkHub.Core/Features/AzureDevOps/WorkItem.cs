namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>An open Azure DevOps work item assigned to the user.</summary>
/// <param name="Id">Work item id (unique across the organization); its string form, prefixed, is the
/// deep-link element id of the item's row on the Work items page.</param>
/// <param name="Title">Title.</param>
/// <param name="Type">Work item type, e.g. <c>Bug</c>, <c>User Story</c>, <c>Task</c>.</param>
/// <param name="State">Workflow state, e.g. <c>Active</c>.</param>
/// <param name="Priority">Priority (1 = highest) as text; empty when the type has none.</param>
/// <param name="DueDate">Due date (UTC) when the process defines one.</param>
/// <param name="Url">Browser URL of the work item form.</param>
/// <param name="Effort">Story points or effort as text; empty when not estimated.</param>
/// <param name="Project">Team project the item lives in — required to query its comments.</param>
public sealed record WorkItem(
    int Id,
    string Title,
    string Type,
    string State,
    string Priority,
    DateTime? DueDate,
    string Url,
    string Effort = "",
    string Project = "");
