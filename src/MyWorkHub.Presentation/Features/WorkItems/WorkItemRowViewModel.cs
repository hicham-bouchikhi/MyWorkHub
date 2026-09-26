using System.Globalization;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Presentation.Features.WorkItems;

/// <summary>One row of the work item list (immutable; a refresh replaces the rows).</summary>
public sealed class WorkItemRowViewModel
{
    private const string DUE_FORMAT = "d MMM";

    public WorkItemRowViewModel(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Id = item.Id;
        Title = item.Title;
        Type = item.Type;
        State = item.State;
        Project = item.Project;
        Url = item.Url;
        IdText = "#" + item.Id.ToString(CultureInfo.InvariantCulture);
        PriorityText = item.Priority.Length > 0 ? "P" + item.Priority : "";
        EffortText = item.Effort.Length > 0 ? item.Effort + " pts" : "";
        DueText = item.DueDate is { } due
            ? "Due " + due.ToLocalTime().ToString(DUE_FORMAT, CultureInfo.CurrentCulture)
            : "";
    }

    public int Id { get; }

    public string Title { get; }

    public string Type { get; }

    public string State { get; }

    public string Project { get; }

    public string Url { get; }

    /// <summary>Azure DevOps' own short form, e.g. <c>#1234</c>.</summary>
    public string IdText { get; }

    /// <summary>e.g. <c>P2</c>; empty when the type has no priority.</summary>
    public string PriorityText { get; }

    /// <summary>e.g. <c>3 pts</c>; empty when not estimated.</summary>
    public string EffortText { get; }

    /// <summary>e.g. <c>Due 3 Oct</c> in the user's culture; empty without a due date.</summary>
    public string DueText { get; }
}
