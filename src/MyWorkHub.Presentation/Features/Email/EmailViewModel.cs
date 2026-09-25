using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.GraphAuth;
using MyWorkHub.Presentation.Navigation;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>
/// The watched mail folders' recent messages, newest first, with a reading pane: selecting a row fetches
/// that message's full body on demand.
/// <para>
/// Deep-link target (same shape as the Todo reference slice): a <see cref="NavigationTarget"/> whose
/// <c>ElementId</c> is a Graph message id (see <see cref="TargetFor"/>) lands on this page with that row
/// selected — so its body loads — and <see cref="HighlightedItem"/> set, which the view's
/// <c>ScrollIntoViewBehavior</c> scrolls to and flashes. A request that arrives before the list has
/// loaded (first visit, or while signed out) is kept pending and applied after the next successful load.
/// </para>
/// </summary>
public sealed partial class EmailViewModel : GraphPageViewModel, IDeepLinkTarget
{
    private readonly IEmailService? _email;
    private string? _pendingFocusId;

    // Bumped on every selection change so a slow body fetch for a row the user already left is ignored.
    private int _bodyRequest;

    public EmailViewModel(IEmailService? email = null, IGraphConnectionService? connection = null)
        : base("Email", email is not null, connection)
    {
        _email = email;
    }

    /// <summary>The navigation target that lands on (and highlights) the given message's row.</summary>
    public static NavigationTarget TargetFor(string emailId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailId);
        return new(typeof(EmailViewModel), emailId);
    }

    /// <summary>Rows, newest first.</summary>
    public ObservableCollection<EmailRowViewModel> Items { get; } = [];

    /// <summary>The row selected in the list (two-way bound to the ListBox); its body is shown in the reading pane.</summary>
    [ObservableProperty]
    private EmailRowViewModel? _selectedItem;

    /// <summary>
    /// The row a deep link asked to reveal. The view's scroll-into-view behavior consumes it (scrolls,
    /// flashes, then writes it back to null), so every request fires exactly once.
    /// </summary>
    [ObservableProperty]
    private EmailRowViewModel? _highlightedItem;

    /// <summary>Plain-text body of <see cref="SelectedItem"/>; empty while loading or when nothing is selected.</summary>
    [ObservableProperty]
    private string _selectedBody = "";

    [ObservableProperty]
    private bool _isBodyLoading;

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

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        var emails = await _email!.GetRecentEmailsAsync(ct);

        // Keep the reader on the same message across a refresh when it is still listed.
        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var email in emails)
        {
            Items.Add(new EmailRowViewModel(email));
        }

        SelectedItem = Items.FirstOrDefault(r => r.Id == selectedId);
    }

    protected override void OnDataLoaded() => ApplyPendingFocus();

    partial void OnSelectedItemChanged(EmailRowViewModel? value)
    {
        var request = ++_bodyRequest;
        SelectedBody = "";
        IsBodyLoading = false;
        if (value is not null && _email is not null)
        {
            _ = LoadBodyAsync(value.Id, request);
        }
    }

    private async Task LoadBodyAsync(string emailId, int request)
    {
        IsBodyLoading = true;
        try
        {
            var body = await _email!.GetEmailBodyAsync(emailId);
            if (request == _bodyRequest)
            {
                SelectedBody = body;
            }
        }
        catch (Exception ex)
        {
            if (request == _bodyRequest)
            {
                ReportFailure(ex);
            }
        }
        finally
        {
            if (request == _bodyRequest)
            {
                IsBodyLoading = false;
            }
        }
    }

    // Unknown ids are dropped silently: a stale notification (message moved/deleted) must not break the page.
    private void ApplyPendingFocus()
    {
        if (_pendingFocusId is not { } elementId)
        {
            return;
        }

        _pendingFocusId = null;
        if (Items.FirstOrDefault(r => r.Id == elementId) is not { } row)
        {
            return;
        }

        SelectedItem = row;
        HighlightedItem = row;
    }
}
