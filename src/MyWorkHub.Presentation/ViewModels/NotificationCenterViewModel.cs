using System.Collections.ObjectModel;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MyWorkHub.Presentation.ViewModels;

/// <summary>
/// Session-only history of notifications shown in the top-bar bell flyout. Every
/// <see cref="INotificationService.Notify"/> also records here (newest first), so a
/// toast that has auto-dismissed can still be reviewed. Held as a singleton and shared
/// between the toast service (writer) and the shell view model (reader).
/// </summary>
public sealed partial class NotificationCenterViewModel : ViewModelBase
{
    private const int MAX_ENTRIES = 200;

    private readonly INavigationService _navigation;

    public NotificationCenterViewModel(INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        _navigation = navigation;
    }

    /// <summary>History, newest first.</summary>
    public ObservableCollection<NotificationEntry> Notifications { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyPropertyChangedFor(nameof(UnreadBadge))]
    private int _unreadCount;

    public bool HasUnread => UnreadCount > 0;

    /// <summary>Badge text, capped so the pill stays small.</summary>
    public string UnreadBadge => UnreadCount > 9 ? "9+" : UnreadCount.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>False when the history is empty (drives the flyout's empty-state text).</summary>
    public bool HasAny => Notifications.Count > 0;

    /// <summary>Operations currently running (e.g. "Repo PR #42"), shown live in the bell with a Stop button.</summary>
    public ObservableCollection<ActiveOperation> ActiveOperations { get; } = [];

    /// <summary>True while at least one operation is in progress (drives the bell's activity indicator).</summary>
    public bool HasActive => ActiveOperations.Count > 0;

    /// <summary>
    /// Marks an operation as started so the bell shows it as in progress. <paramref name="onCancel"/>
    /// is invoked when the user clicks the operation's Stop button. Returns the handle to pass back to
    /// <see cref="EndActivity"/> when the operation finishes.
    /// </summary>
    public ActiveOperation BeginActivity(string label, Action onCancel)
    {
        var operation = new ActiveOperation(label, onCancel);
        ActiveOperations.Add(operation);
        OnPropertyChanged(nameof(HasActive));
        return operation;
    }

    /// <summary>Clears an in-progress operation (any outcome). Safe to call for an already-removed handle.</summary>
    public void EndActivity(ActiveOperation operation)
    {
        ActiveOperations.Remove(operation);
        OnPropertyChanged(nameof(HasActive));
    }

    /// <summary>
    /// Records a notification at the top of the history and returns it (so the toast can share the
    /// entry's activation path). Assumes the UI thread — the toast service marshals via the
    /// dispatcher before calling this, so it stays dispatcher-free and directly unit-testable.
    /// </summary>
    public NotificationEntry Add(string title, string message, NotificationSeverity severity, NavigationTarget? target = null)
    {
        var entry = new NotificationEntry(title, message, severity, DateTime.Now, _navigation, target);
        Notifications.Insert(0, entry);

        while (Notifications.Count > MAX_ENTRIES)
        {
            Notifications.RemoveAt(Notifications.Count - 1);
        }

        UnreadCount++;
        OnPropertyChanged(nameof(HasAny));
        return entry;
    }

    [RelayCommand]
    private void MarkAllRead()
    {
        foreach (var entry in Notifications)
        {
            entry.IsRead = true;
        }

        UnreadCount = 0;
    }

    [RelayCommand]
    private void Clear()
    {
        Notifications.Clear();
        UnreadCount = 0;
        OnPropertyChanged(nameof(HasAny));
    }
}
