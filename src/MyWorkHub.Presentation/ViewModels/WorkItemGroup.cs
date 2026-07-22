namespace MyWorkHub.UI.ViewModels;

/// <summary>Work items sharing a state (Active / New / Resolved / …), for the grouped list.</summary>
public sealed class WorkItemGroup
{
    public WorkItemGroup(string name, IReadOnlyList<WorkItemRow> items)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(items);
        Name = name;
        Items = items;
    }

    public string Name { get; }

    public IReadOnlyList<WorkItemRow> Items { get; }

    public int Count => Items.Count;
}
