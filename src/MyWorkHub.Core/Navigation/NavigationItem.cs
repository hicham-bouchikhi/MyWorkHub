namespace MyWorkHub.Core.Navigation;

/// <summary>
/// A single sidebar entry contributed by a feature's presentation module: the label and icon shown
/// in the nav rail, plus the page view-model type navigated to when the entry is selected.
/// </summary>
/// <param name="Label">Text shown next to the icon when the sidebar is expanded.</param>
/// <param name="Icon">Glyph shown in the nav rail (always visible, even when collapsed).</param>
/// <param name="ViewModelType">The page view-model type displayed when this item is selected.</param>
/// <param name="Order">Sort key within the sidebar (ascending; ties broken by label). The shell lands
/// on the first entry at startup.</param>
public sealed record NavigationItem(string Label, string Icon, Type ViewModelType, int Order = 0)
{
    public string Label { get; init; } = !string.IsNullOrWhiteSpace(Label)
        ? Label
        : throw new ArgumentException("A navigation item needs a label.", nameof(Label));

    public Type ViewModelType { get; init; } = ViewModelType ?? throw new ArgumentNullException(nameof(ViewModelType));
}
