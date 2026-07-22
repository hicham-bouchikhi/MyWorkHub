using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Navigation;

/// <inheritdoc cref="INavigationService" />
public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;

    // One instance per page type, kept for the app's lifetime. Navigating away and back
    // returns the SAME view model, so page state survives — in particular an in-progress
    // PR review (its spinner, activity log and cancellation) keeps running and stays visible,
    // and pages don't re-fetch on every tab switch.
    private readonly Dictionary<Type, ViewModelBase> _pages = [];

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public NavigationService(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public void NavigateTo(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        if (!_pages.TryGetValue(viewModelType, out var page))
        {
            page = (ViewModelBase)_services.GetRequiredService(viewModelType);
            _pages[viewModelType] = page;
        }

        CurrentPage = page;
    }
}
