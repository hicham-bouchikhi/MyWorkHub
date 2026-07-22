using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.UI.Navigation;

namespace MyWorkHub.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const double EXPANDED_SIDEBAR_WIDTH = 220;
    private const double COLLAPSED_SIDEBAR_WIDTH = 56;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    public MainWindowViewModel(INavigationService navigation, NotificationCenterViewModel notifications)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(notifications);
        Navigation = navigation;
        Notifications = notifications;
        Navigation.PropertyChanged += OnNavigationPropertyChanged;

        NavigationItems = new[]
        {
            new NavigationItem("Dashboard", "\U0001F3E0", typeof(DashboardViewModel)),
            new NavigationItem("Email", "✉", typeof(EmailViewModel)),
            new NavigationItem("Pull Requests", "\U0001F500", typeof(PullRequestsViewModel)),
            new NavigationItem("Work Items", "\U0001F4CB", typeof(WorkItemsViewModel)),
            new NavigationItem("Teams", "\U0001F4AC", typeof(TeamsViewModel)),
            new NavigationItem("Todo", "✅", typeof(TodoViewModel)),
            new NavigationItem("Remote Work", "\U0001F3E1", typeof(RemoteWorkViewModel)),
            new NavigationItem("Automations", "⚙", typeof(AutomationsViewModel)),
            new NavigationItem("Settings", "\U0001F527", typeof(SettingsViewModel)),
        };

        BottomNavItems = new[] { new NavigationItem("Developer", "🛠", typeof(DevViewModel)) };

        // Land on the Dashboard; setting SelectedItem triggers the initial navigation.
        SelectedItem = NavigationItems[0];
    }

    /// <summary>The active page, bound to the content area.</summary>
    public INavigationService Navigation { get; }

    /// <summary>Notification history + unread count backing the top-bar bell.</summary>
    public NotificationCenterViewModel Notifications { get; }

    /// <summary>Main sidebar entries.</summary>
    public IReadOnlyList<NavigationItem> NavigationItems { get; }

    /// <summary>Items pinned at the bottom of the sidebar (dev tools, etc.).</summary>
    public IReadOnlyList<NavigationItem> BottomNavItems { get; }

    /// <summary>Current sidebar width, driven by <see cref="IsSidebarExpanded"/>.</summary>
    public double SidebarWidth => IsSidebarExpanded ? EXPANDED_SIDEBAR_WIDTH : COLLAPSED_SIDEBAR_WIDTH;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        // Skip if the content area already shows this page (e.g. synced from NavigationService).
        if (value is not null && Navigation.CurrentPage?.GetType() != value.ViewModelType)
        {
            Navigation.NavigateTo(value.ViewModelType);
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(INavigationService.CurrentPage))
            return;

        var pageType = Navigation.CurrentPage?.GetType();
        if (pageType is null)
            return;

        var match = NavigationItems.FirstOrDefault(i => i.ViewModelType == pageType)
                 ?? BottomNavItems.FirstOrDefault(i => i.ViewModelType == pageType);
        if (match is not null && !ReferenceEquals(match, SelectedItem))
            SelectedItem = match;
    }
}
