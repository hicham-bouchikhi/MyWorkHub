using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Presentation.Navigation;

/// <inheritdoc cref="INavigationService" />
public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _services;

    // One instance per page type, kept for the app's lifetime. Navigating away and back
    // returns the SAME view model, so page state (in-progress work, scroll position, loaded
    // data) survives and pages don't re-fetch on every tab switch.
    private readonly Dictionary<Type, ViewModelBase> _pages = [];

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public NavigationService(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public void NavigateTo(NavigationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var pageType = target.ViewModelType;
        if (!_pages.TryGetValue(pageType, out var page))
        {
            page = _services.GetRequiredService(pageType) as ViewModelBase
                ?? throw new InvalidOperationException(
                    $"Cannot navigate to '{pageType.FullName}': it does not derive from {nameof(ViewModelBase)}.");
            _pages[pageType] = page;
        }

        CurrentPage = page;

        if (target.ElementId is { } elementId && page is IDeepLinkTarget deepLink)
        {
            deepLink.FocusElement(elementId);
        }
    }
}
