using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Navigation;

/// <summary>
/// A single sidebar entry: the label and icon shown in the nav rail, plus the
/// page <see cref="ViewModelBase"/> type that <see cref="INavigationService"/> resolves
/// and displays when the entry is selected.
/// </summary>
public sealed class NavigationItem
{
    public NavigationItem(string label, string icon, Type viewModelType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(viewModelType);

        Label = label;
        Icon = icon;
        ViewModelType = viewModelType;
    }

    /// <summary>Text shown next to the icon when the sidebar is expanded.</summary>
    public string Label { get; }

    /// <summary>Glyph shown in the nav rail (always visible, even when collapsed).</summary>
    public string Icon { get; }

    /// <summary>The page view-model type displayed when this item is selected.</summary>
    public Type ViewModelType { get; }
}
