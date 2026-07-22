using System.Globalization;
using MyWorkHub.Core.Models;

namespace MyWorkHub.UI.ViewModels;

/// <summary>A work item projected for the Work Items list (display-only).</summary>
public sealed class WorkItemRow
{
    private static readonly HashSet<string> _doneStates =
        new(StringComparer.OrdinalIgnoreCase) { "Resolved", "Closed", "Done", "Completed", "Removed" };

    private WorkItemRow(WorkItem item)
    {
        WorkItemId = item.Id;
        Title = item.Title;
        Type = item.Type;
        State = item.State;
        Priority = item.Priority;
        Effort = item.Effort;
        Url = item.Url;
        TypeIcon = IconFor(item.Type);

        DueText = item.DueDate is { } due ? due.ToString("d", CultureInfo.CurrentCulture) : "";
        IsOverdue = item.DueDate is { } d && d.Date < DateTime.Today && !_doneStates.Contains(item.State);
    }

    public int WorkItemId { get; }

    public string Title { get; }

    public string Type { get; }

    public string TypeIcon { get; }

    public string State { get; }

    public string Priority { get; }

    public string Effort { get; }

    public string DueText { get; }

    public bool IsOverdue { get; }

    public string Url { get; }

    /// <summary>
    /// Semantic badge category for the work-item state, used to pick a palette brush via
    /// <c>Classes.&lt;category&gt;</c> styling. One of: info | success | danger | neutral.
    /// </summary>
    public string BadgeCategory => State switch
    {
        "Active" or "Committed" or "In Progress" or "Doing" => "info",
        "Resolved" or "Closed" or "Done" or "Completed" => "success",
        "Removed" => "danger",
        _ => "neutral",
    };

    public bool IsBadgeInfo => BadgeCategory == "info";

    public bool IsBadgeSuccess => BadgeCategory == "success";

    public bool IsBadgeDanger => BadgeCategory == "danger";

    public bool IsBadgeNeutral => BadgeCategory == "neutral";

    public static WorkItemRow From(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WorkItemRow(item);
    }

    private static string IconFor(string type) => type switch
    {
        "Bug" => "🐞",
        "User Story" or "Product Backlog Item" => "📖",
        "Task" => "🗒",
        "Feature" => "★",
        "Epic" => "🏔",
        "Issue" => "⚠",
        _ => "📋",
    };
}
