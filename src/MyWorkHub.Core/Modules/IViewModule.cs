namespace MyWorkHub.Core.Modules;

/// <summary>
/// One feature's explicit view-model → view map (e.g. <c>typeof(EmailViewModel) → typeof(EmailView)</c>).
/// Declared here so the contract carries no UI-toolkit types, but only ever implemented inside
/// <c>MyWorkHub.UI</c> — the one project allowed to reference concrete view types. All discovered
/// maps are merged into a single registry the view locator reads from; a page view model with no
/// entry fails startup validation instead of silently rendering a placeholder.
/// Implementations must be non-abstract classes with a public parameterless constructor.
/// </summary>
public interface IViewModule
{
    /// <summary>Page view-model type → the view type that renders it.</summary>
    IReadOnlyDictionary<Type, Type> ViewModelToView { get; }
}
