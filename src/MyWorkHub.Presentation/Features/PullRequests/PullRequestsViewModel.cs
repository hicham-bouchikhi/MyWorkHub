using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.PrReview;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.AzureDevOps;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.PullRequests;

/// <summary>
/// Active pull requests the user created or reviews, newest first; a row opens in the browser.
/// <para>
/// AI review (optional, when <see cref="IPrReviewService"/> is registered): a row's Review button runs a Claude
/// review of that pull request — or, with "Review with…", one using a review-agent file picked for this review
/// only. One review runs at a time; its progress streams into <see cref="ReviewOutput"/>, it shows in the bell's
/// "In progress" list with a Stop button, and completion raises a notification that leads back to the row.
/// </para>
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
    private static readonly string[] _agentFileExtensions = [".md"];

    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly IBrowserLauncher? _browser;
    private readonly IPrReviewService? _prReview;
    private readonly IFilePicker? _filePicker;
    private readonly INotificationService? _notifications;
    private readonly NotificationCenterViewModel? _notificationCenter;
    private CancellationTokenSource? _reviewCancellation;
    private string? _pendingFocusId;

    public PullRequestsViewModel(
        IAzureDevOpsService? azureDevOps = null,
        IAzureDevOpsConnectionService? connection = null,
        IBrowserLauncher? browser = null,
        IPrReviewService? prReview = null,
        IFilePicker? filePicker = null,
        INotificationService? notifications = null,
        NotificationCenterViewModel? notificationCenter = null)
        : base("Pull requests", azureDevOps is not null, connection)
    {
        _azureDevOps = azureDevOps;
        _browser = browser;
        _prReview = prReview;
        _filePicker = filePicker;
        _notifications = notifications;
        _notificationCenter = notificationCenter;
    }

    /// <summary>Whether AI review is registered (drives the Review buttons' visibility).</summary>
    public bool IsReviewAvailable => _prReview is not null && IsAvailable;

    /// <summary>Whether a review-agent file can be picked for a single review.</summary>
    public bool CanPickReviewAgent => IsReviewAvailable && _filePicker is not null;

    /// <summary>True while a review runs; disables starting another one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReviewWithAgentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelReviewCommand))]
    private bool _isReviewing;

    /// <summary>The pull request of the running or last review; null before the first one.</summary>
    [ObservableProperty]
    private PullRequestRowViewModel? _reviewedItem;

    /// <summary>Streaming progress (git steps, then the agent's output) while running; the Markdown report once done.</summary>
    [ObservableProperty]
    private string _reviewOutput = "";

    [ObservableProperty]
    private string? _reviewError;

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

    [RelayCommand(CanExecute = nameof(CanReview))]
    private Task ReviewAsync(PullRequestRowViewModel row) => RunReviewAsync(row, agentFilePath: null);

    /// <summary>Asks for a review-agent Markdown file, then reviews with it (this review only).</summary>
    [RelayCommand(CanExecute = nameof(CanReviewWithAgent))]
    private async Task ReviewWithAgentAsync(PullRequestRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var agentFilePath = await _filePicker!.PickFileAsync("Choose a review agent for " + row.IdText, extensions: _agentFileExtensions);
        if (agentFilePath is not null)
        {
            await RunReviewAsync(row, agentFilePath);
        }
    }

    [RelayCommand(CanExecute = nameof(IsReviewing))]
    private void CancelReview() => _reviewCancellation?.Cancel();

    private bool CanReview(PullRequestRowViewModel? row) => row is not null && _prReview is not null && !IsReviewing;

    private bool CanReviewWithAgent(PullRequestRowViewModel? row) => CanReview(row) && _filePicker is not null;

    private async Task RunReviewAsync(PullRequestRowViewModel row, string? agentFilePath)
    {
        ArgumentNullException.ThrowIfNull(row);

        using var cancellation = new CancellationTokenSource();
        _reviewCancellation = cancellation;
        IsReviewing = true;
        ReviewedItem = row;
        ReviewOutput = "";
        ReviewError = null;
        var activity = _notificationCenter?.BeginActivity("Reviewing " + row.IdText, CancelReview);

        // Progress<T> posts asynchronously; a line arriving after the run ended must not overwrite the report.
        var running = true;
        var progress = new Progress<string>(line =>
        {
            if (running)
            {
                ReviewOutput += line + Environment.NewLine;
            }
        });
        try
        {
            var report = await _prReview!.ReviewAsync(row.Item, progress, agentFilePath, cancellation.Token);
            running = false;
            ReviewOutput = report;
            _notifications?.Notify("Review ready", $"{row.IdText} {row.Title}", NotificationSeverity.SUCCESS, TargetFor(row.Id));
        }
        catch (OperationCanceledException)
        {
            running = false;
            ReviewError = "Review cancelled.";
        }
        catch (Exception ex)
        {
            running = false;
            ReviewError = $"Review failed: {ex.Message}";
            _notifications?.Notify("Review failed", $"{row.IdText}: {ex.Message}", NotificationSeverity.ERROR, TargetFor(row.Id));
        }
        finally
        {
            if (activity is not null)
            {
                _notificationCenter!.EndActivity(activity);
            }

            _reviewCancellation = null;
            IsReviewing = false;
        }
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
