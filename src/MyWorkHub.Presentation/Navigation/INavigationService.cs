using System.ComponentModel;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Navigation;

/// <summary>
/// Hand-written page switching for the main content area (no ReactiveUI routing).
/// Resolves page view-models from the DI container on demand and exposes the active
/// one as <see cref="CurrentPage"/>.
/// </summary>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>The view-model currently shown in the content area, or null before first navigation.</summary>
    ViewModelBase? CurrentPage { get; }

    /// <summary>
    /// Makes the target's page current (resolving it from DI on first visit), then — when the target
    /// names an element and the page implements <see cref="IDeepLinkTarget"/> — focuses that element.
    /// </summary>
    void NavigateTo(NavigationTarget target);
}
