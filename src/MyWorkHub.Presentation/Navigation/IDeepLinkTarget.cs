namespace MyWorkHub.Presentation.Navigation;

/// <summary>
/// Implemented by a page view model that can land on one specific element (e.g. a single work item
/// or email row). After navigating to such a page with a <see cref="Core.Navigation.NavigationTarget"/>
/// whose <c>ElementId</c> is set, <see cref="INavigationService"/> calls <see cref="FocusElement"/>.
/// </summary>
public interface IDeepLinkTarget
{
    /// <summary>Selects / highlights the element with the given id; unknown ids are ignored.</summary>
    void FocusElement(string elementId);
}
