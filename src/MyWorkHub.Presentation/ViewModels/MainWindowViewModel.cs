using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;

namespace MyWorkHub.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const double EXPANDED_SIDEBAR_WIDTH = 220;
    private const double COLLAPSED_SIDEBAR_WIDTH = 56;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    /// <param name="navigation">Page switching for the content area.</param>
    /// <param name="notifications">The shared bell-flyout history.</param>
    /// <param name="navigationItems">The sidebar, built from every discovered presentation module's
    /// menu entry (already sorted) — the shell holds no hardcoded page list.</param>
    public MainWindowViewModel(
        INavigationService navigation,
        NotificationCenterViewModel notifications,
        IReadOnlyList<NavigationItem> navigationItems)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(notifications);
        ArgumentNullException.ThrowIfNull(navigationItems);

        Navigation = navigation;
        Notifications = notifications;
        NavigationItems = navigationItems;
        Navigation.PropertyChanged += OnNavigationPropertyChanged;

        // Land on the first entry (if any); setting SelectedItem triggers the initial navigation.
        SelectedItem = NavigationItems.Count > 0 ? NavigationItems[0] : null;
    }

    /// <summary>The active page, bound to the content area.</summary>
    public INavigationService Navigation { get; }

    /// <summary>Notification history + unread count backing the top-bar bell.</summary>
    public NotificationCenterViewModel Notifications { get; }

    /// <summary>Sidebar entries, one per feature module that contributes a menu entry.</summary>
    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    /// <summary>Current sidebar width, driven by <see cref="IsSidebarExpanded"/>.</summary>
    public double SidebarWidth => IsSidebarExpanded ? EXPANDED_SIDEBAR_WIDTH : COLLAPSED_SIDEBAR_WIDTH;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        // Skip if the content area already shows this page (e.g. synced from NavigationService).
        if (value is not null && Navigation.CurrentPage?.GetType() != value.ViewModelType)
        {
            Navigation.NavigateTo(new NavigationTarget(value.ViewModelType));
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(INavigationService.CurrentPage))
            return;

        var pageType = Navigation.CurrentPage?.GetType();
        if (pageType is null)
            return;

        // Keep the sidebar highlight in sync when navigation happens elsewhere (e.g. a notification).
        var match = NavigationItems.FirstOrDefault(i => i.ViewModelType == pageType);
        if (match is not null && !ReferenceEquals(match, SelectedItem))
            SelectedItem = match;
    }
}
