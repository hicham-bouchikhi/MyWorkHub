using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.AzureDevOps;
using MyWorkHub.Presentation.Navigation;

namespace MyWorkHub.Presentation.Features.WorkItems;

/// <summary>
/// The user's open work items plus a Mentions section listing comments on them that @mention the user
/// (unread until opened; read state persisted via <see cref="ISeenMentionRepository"/>).
/// <para>
/// Mentions are best-effort: the comments endpoint is a preview-only Azure DevOps API, so the service
/// skips any work item whose comments cannot be read, and this page additionally wraps the whole mentions
/// step — a failure there only hides the Mentions section, never the work items.
/// </para>
/// <para>
/// Deep-link target with TWO element kinds, told apart by a prefix on <c>ElementId</c>:
/// <list type="bullet">
/// <item><c>item:{workItemId}</c> — a work item row (<see cref="TargetForWorkItem"/>);</item>
/// <item><c>mention:{commentId}</c> — a mention row (<see cref="TargetForMention"/>).</item>
/// </list>
/// Ids are invariant-culture digits. The prefix is required: a bare number is ambiguous (work item and
/// comment ids overlap) and, like any unknown or stale id, is ignored. Each list has its own
/// selected/highlighted pair, bound to its own <c>ScrollIntoViewBehavior</c>. A request that arrives
/// before the page has loaded (first visit, or while no token is stored) is kept pending and applied after
/// the next successful load — work items and mentions both.
/// </para>
/// </summary>
public sealed partial class WorkItemsViewModel : AzureDevOpsPageViewModel, IDeepLinkTarget
{
    // ElementId prefixes of the two deep-link kinds (see the class remarks). Callers build targets
    // through TargetForWorkItem / TargetForMention rather than concatenating these.
    private const string WORK_ITEM_ELEMENT_PREFIX = "item:";
    private const string MENTION_ELEMENT_PREFIX = "mention:";

    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly ISeenMentionRepository? _seenMentions;
    private readonly IBrowserLauncher? _browser;
    private string? _pendingFocusId;

    public WorkItemsViewModel(
        IAzureDevOpsService? azureDevOps = null,
        IAzureDevOpsConnectionService? connection = null,
        ISeenMentionRepository? seenMentions = null,
        IBrowserLauncher? browser = null)
        : base("Work items", azureDevOps is not null, connection)
    {
        _azureDevOps = azureDevOps;
        _seenMentions = seenMentions;
        _browser = browser;
    }

    /// <summary>The navigation target that lands on (and highlights) the given work item's row.</summary>
    public static NavigationTarget TargetForWorkItem(int workItemId)
        => new(typeof(WorkItemsViewModel), WORK_ITEM_ELEMENT_PREFIX + workItemId.ToString(CultureInfo.InvariantCulture));

    /// <summary>The navigation target that lands on (and highlights) the given mention's row.</summary>
    public static NavigationTarget TargetForMention(int commentId)
        => new(typeof(WorkItemsViewModel), MENTION_ELEMENT_PREFIX + commentId.ToString(CultureInfo.InvariantCulture));

    /// <summary>Work item rows, most recently changed first.</summary>
    public ObservableCollection<WorkItemRowViewModel> Items { get; } = [];

    /// <summary>Mention rows, newest first; empty (section hidden) when mentions could not be loaded.</summary>
    public ObservableCollection<WorkItemMentionRowViewModel> Mentions { get; } = [];

    [ObservableProperty]
    private WorkItemRowViewModel? _selectedItem;

    /// <summary>The work item row a deep link asked to reveal; consumed (reset to null) by the view's behavior.</summary>
    [ObservableProperty]
    private WorkItemRowViewModel? _highlightedItem;

    [ObservableProperty]
    private WorkItemMentionRowViewModel? _selectedMention;

    /// <summary>The mention row a deep link asked to reveal; consumed (reset to null) by the view's behavior.</summary>
    [ObservableProperty]
    private WorkItemMentionRowViewModel? _highlightedMention;

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
    private void OpenWorkItem(WorkItemRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _browser?.Open(row.Url);
    }

    /// <summary>Opens the commented work item in the browser and marks the mention read.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenMentionAsync(WorkItemMentionRowViewModel row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);

        _browser?.Open(row.WorkItemUrl);
        if (!row.IsUnread)
        {
            return;
        }

        row.IsUnread = false;
        if (_seenMentions is null)
        {
            return;
        }

        try
        {
            await _seenMentions.MarkSeenAsync(row.CommentId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Read state is a convenience: failing to persist it only means the mention shows as
            // unread again next session. The user got what they clicked for — don't raise an error.
        }
    }

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        var workItems = await _azureDevOps!.GetMyWorkItemsAsync(ct);

        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var workItem in workItems)
        {
            Items.Add(new WorkItemRowViewModel(workItem));
        }

        SelectedItem = Items.FirstOrDefault(r => r.Id == selectedId);

        await LoadMentionsAsync(workItems, ct);
    }

    protected override void OnDataLoaded() => ApplyPendingFocus();

    // Outer best-effort guard around the whole mentions step (the service already skips individual work
    // items): whatever fails here — the preview comments API, the identity lookup, the local read-state
    // store — hides the Mentions section and leaves the work items untouched.
    private async Task LoadMentionsAsync(IReadOnlyList<WorkItem> workItems, CancellationToken ct)
    {
        IReadOnlyList<WorkItemMention> mentions;
        IReadOnlySet<int> seen;
        try
        {
            mentions = await _azureDevOps!.GetWorkItemMentionsAsync(workItems, ct);
            seen = _seenMentions is null ? new HashSet<int>() : await _seenMentions.GetSeenCommentIdsAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            Mentions.Clear();
            SelectedMention = null;
            return;
        }

        var selectedCommentId = SelectedMention?.CommentId;
        Mentions.Clear();
        foreach (var mention in mentions)
        {
            Mentions.Add(new WorkItemMentionRowViewModel(mention, isUnread: !seen.Contains(mention.CommentId)));
        }

        SelectedMention = Mentions.FirstOrDefault(r => r.CommentId == selectedCommentId);
    }

    // Unknown, stale or malformed ids are dropped silently: a stale notification must not break the page.
    private void ApplyPendingFocus()
    {
        if (_pendingFocusId is not { } elementId)
        {
            return;
        }

        _pendingFocusId = null;
        if (TryParseId(elementId, WORK_ITEM_ELEMENT_PREFIX, out var workItemId))
        {
            if (Items.FirstOrDefault(r => r.Id == workItemId) is { } row)
            {
                SelectedItem = row;
                HighlightedItem = row;
            }
        }
        else if (TryParseId(elementId, MENTION_ELEMENT_PREFIX, out var commentId)
                 && Mentions.FirstOrDefault(r => r.CommentId == commentId) is { } mention)
        {
            SelectedMention = mention;
            HighlightedMention = mention;
        }
    }

    private static bool TryParseId(string elementId, string prefix, out int id)
    {
        id = 0;
        return elementId.StartsWith(prefix, StringComparison.Ordinal)
               && int.TryParse(elementId.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }
}
