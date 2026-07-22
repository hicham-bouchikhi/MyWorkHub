using System.ComponentModel;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Navigation;

/// <summary>
/// Hand-written page switching for the main content area (no ReactiveUI routing).
/// Resolves page view-models from the DI container on demand and exposes the active
/// one as <see cref="CurrentPage"/>.
/// </summary>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>The view-model currently shown in the content area, or null before first navigation.</summary>
    ViewModelBase? CurrentPage { get; }

    /// <summary>Resolves the page of the given type from DI and makes it the current page.</summary>
    void NavigateTo(Type viewModelType);
}
