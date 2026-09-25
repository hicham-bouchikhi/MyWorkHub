using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.AzureDevOps;
using MyWorkHub.Presentation.Navigation;

namespace MyWorkHub.Presentation.Features.PullRequests;

/// <summary>
/// Active pull requests the user created or reviews, newest first; a row opens in the browser.
/// <para>
/// Deep-link target (same shape as the Todo reference slice): a <see cref="NavigationTarget"/> whose
/// <c>ElementId</c> is a pull request id in invariant digits (see <see cref="TargetFor"/>) lands on this
/// page with that row selected and <see cref="HighlightedItem"/> set, which the view's
/// <c>ScrollIntoViewBehavior</c> scrolls to and flashes. A request that arrives before the list has loaded
/// (first visit, or while no token is stored) is kept pending and applied after the next successful load.
/// </para>
/// </summary>
public sealed partial class PullRequestsViewModel : AzureDevOpsPageViewModel, IDeepLinkTarget
{
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly IBrowserLauncher? _browser;
    private string? _pendingFocusId;

    public PullRequestsViewModel(
        IAzureDevOpsService? azureDevOps = null,
        IAzureDevOpsConnectionService? connection = null,
        IBrowserLauncher? browser = null)
        : base("Pull requests", azureDevOps is not null, connection)
    {
        _azureDevOps = azureDevOps;
        _browser = browser;
    }

    /// <summary>The navigation target that lands on (and highlights) the given pull request's row.</summary>
    public static NavigationTarget TargetFor(int pullRequestId)
        => new(typeof(PullRequestsViewModel), pullRequestId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Rows, newest first.</summary>
    public ObservableCollection<PullRequestRowViewModel> Items { get; } = [];

    /// <summary>The row selected in the list (two-way bound to the ListBox).</summary>
    [ObservableProperty]
    private PullRequestRowViewModel? _selectedItem;

    /// <summary>
    /// The row a deep link asked to reveal. The view's scroll-into-view behavior consumes it (scrolls,
    /// flashes, then writes it back to null), so every request fires exactly once.
    /// </summary>
    [ObservableProperty]
    private PullRequestRowViewModel? _highlightedItem;

    /// <inheritdoc />
    public void FocusElement(string elementId)
    {
        ArgumentNullException.ThrowIfNull(elementId);

        _pendingFocusId = elementId;
        if (HasLoaded)
        {
            ApplyPendingFocus();
        }
    }

    [RelayCommand]
    private void Open(PullRequestRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _browser?.Open(row.Url);
    }

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        var pullRequests = await _azureDevOps!.GetMyPullRequestsAsync(ct);

        // Keep the same pull request selected across a refresh when it is still listed.
        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var pullRequest in pullRequests)
        {
            Items.Add(new PullRequestRowViewModel(pullRequest));
        }

        SelectedItem = Items.FirstOrDefault(r => r.Id == selectedId);
    }

    protected override void OnDataLoaded() => ApplyPendingFocus();

    // Unknown or malformed ids are dropped silently: a stale notification (pull request completed or
    // abandoned since) must not break the page.
    private void ApplyPendingFocus()
    {
        if (_pendingFocusId is not { } elementId)
        {
            return;
        }

        _pendingFocusId = null;
        if (!int.TryParse(elementId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || Items.FirstOrDefault(r => r.Id == id) is not { } row)
        {
            return;
        }

        SelectedItem = row;
        HighlightedItem = row;
    }
}
