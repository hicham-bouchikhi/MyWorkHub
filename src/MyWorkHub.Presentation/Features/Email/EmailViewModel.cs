using MyWorkHub.Core.Abstractions;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.EmailSummary;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.GraphAuth;
using MyWorkHub.Presentation.Navigation;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>
/// A mail client: the mailbox's folder tree (topped by a virtual "Favorites" entry merging the folders chosen in
/// Settings → Email), the selected folder's newest messages, and a reading pane that renders the selected message's HTML
/// sandboxed (<see cref="EmailHtmlDocument"/>): remote images stay blocked until <see cref="LoadImagesCommand"/>,
/// and links go to the default browser (<see cref="OpenLink"/>). When the AI digest is available,
/// <see cref="SummarizeCommand"/> summarizes the listed messages and <see cref="SummarizeSelectedCommand"/> only
/// the open one (streaming progress into <see cref="SummaryStatus"/>).
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
    private readonly IEmailSummaryService? _summary;
    private readonly IBrowserLauncher? _browser;
    private readonly ISettingsService? _settings;
    private readonly MailFolderViewModel _favorites = MailFolderViewModel.Favorites();
    private bool _isRebuildingFolders;
    private IReadOnlyList<EmailItem> _emails = [];
    private string? _pendingFocusId;

    // Bumped on every selection change so a slow body fetch for a row the user already left is ignored.
    private int _bodyRequest;

    public EmailViewModel(
        IEmailService? email = null,
        IGraphConnectionService? connection = null,
        IEmailSummaryService? summary = null,
        IBrowserLauncher? browser = null,
        ISettingsService? settings = null)
        : base("Email", email is not null, connection)
    {
        _email = email;
        _summary = summary;
        _browser = browser;
        _settings = settings;

        // Until the tree loads (or if it cannot), the merged Favorites list is what is shown.
        _selectedFolder = _favorites;
    }

    /// <summary>Whether the AI digest is registered (drives the Summarize button's visibility).</summary>
    public bool IsSummaryAvailable => _summary is not null && IsAvailable;

    /// <summary>The navigation target that lands on (and highlights) the given message's row.</summary>
    public static NavigationTarget TargetFor(string emailId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailId);
        return new(typeof(EmailViewModel), emailId);
    }

    /// <summary>
    /// The folder tree: the mailbox's top-level folders (Outlook order), preceded by "Favorites" when
    /// Settings → Email enables it.
    /// </summary>
    public ObservableCollection<MailFolderViewModel> Folders { get; } = [];

    /// <summary>The folder whose messages are listed (two-way bound to the TreeView).</summary>
    [ObservableProperty]
    private MailFolderViewModel? _selectedFolder;

    /// <summary>Rows, newest first.</summary>
    public ObservableCollection<EmailRowViewModel> Items { get; } = [];

    /// <summary>The row selected in the list (two-way bound to the ListBox); its body is shown in the reading pane.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SummarizeSelectedCommand))]
    private EmailRowViewModel? _selectedItem;

    /// <summary>
    /// The row a deep link asked to reveal. The view's scroll-into-view behavior consumes it (scrolls,
    /// flashes, then writes it back to null), so every request fires exactly once.
    /// </summary>
    [ObservableProperty]
    private EmailRowViewModel? _highlightedItem;

    /// <summary>The sender's HTML for <see cref="SelectedItem"/>; empty while loading or when nothing is selected.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDocument), nameof(HasBlockedImages))]
    private string _selectedHtml = "";

    /// <summary>Whether the open message may load its remote images; reset for every message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDocument), nameof(HasBlockedImages))]
    private bool _areRemoteImagesAllowed;

    /// <summary>The sandboxed document the reading pane renders.</summary>
    public string SelectedDocument => EmailHtmlDocument.Build(SelectedHtml, AreRemoteImagesAllowed);

    /// <summary>Whether the open message has remote images that are currently blocked (shows "Load images").</summary>
    public bool HasBlockedImages => !AreRemoteImagesAllowed && EmailHtmlDocument.HasRemoteImages(SelectedHtml);

    [ObservableProperty]
    private bool _isBodyLoading;

    /// <summary>The last digest (Markdown text); null until one has been produced.</summary>
    [ObservableProperty]
    private string? _summaryText;

    /// <summary>Latest progress line of the running digest (body fetching, then the agent's output).</summary>
    [ObservableProperty]
    private string? _summaryStatus;

    [ObservableProperty]
    private string? _summaryError;

    /// <summary>Summarizes the listed messages with the AI agent. Cancellable via <c>SummarizeCancelCommand</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanSummarize), IncludeCancelCommand = true)]
    private Task SummarizeAsync(CancellationToken ct) => RunSummaryAsync(_emails, ct);

    /// <summary>Summarizes only the open message. Cancellable via <c>SummarizeSelectedCancelCommand</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanSummarizeSelected), IncludeCancelCommand = true)]
    private Task SummarizeSelectedAsync(CancellationToken ct) => RunSummaryAsync([SelectedItem!.Item], ct);

    private bool CanSummarizeSelected() => _summary is not null && SelectedItem is not null;

    /// <summary>Lets the open message load its remote images.</summary>
    [RelayCommand]
    private void LoadImages() => AreRemoteImagesAllowed = true;

    /// <summary>
    /// Opens a link the user followed in the reading pane in the default browser. Only web and mail links are
    /// honoured: anything else (file:, javascript:, custom schemes) from untrusted mail is dropped.
    /// </summary>
    public void OpenLink(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.IsAbsoluteUri && uri.Scheme is "http" or "https" or "mailto")
        {
            _browser?.Open(uri.OriginalString);
        }
    }

    /// <summary>Lists the newly selected folder (the tree is kept as is).</summary>
    [RelayCommand]
    private async Task LoadFolderAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            await LoadMessagesAsync(ct);
            ErrorMessage = null;
            ApplyPendingFocus();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded: the command cancels a still-running execution when it is executed again (e.g. the
            // page is revisited mid-load). The newer run owns the page; rethrowing would crash the dispatcher.
            return;
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunSummaryAsync(IReadOnlyList<EmailItem> emails, CancellationToken ct)
    {
        SummaryError = null;
        SummaryStatus = "Starting…";

        // Progress<T> posts asynchronously; a line arriving after the run ended must not overwrite the outcome.
        var running = true;
        var progress = new Progress<string>(line =>
        {
            if (running)
            {
                SummaryStatus = line;
            }
        });
        try
        {
            var digest = await _summary!.SummarizeAsync(emails, progress, ct);
            running = false;
            SummaryText = digest;
            SummaryStatus = null;
        }
        catch (OperationCanceledException)
        {
            running = false;
            SummaryStatus = "Summary cancelled.";
        }
        catch (GraphNotConnectedException ex)
        {
            running = false;
            SummaryStatus = null;
            ReportFailure(ex);
        }
        catch (Exception ex)
        {
            running = false;
            SummaryStatus = null;
            SummaryError = $"Could not summarize: {ex.Message}";
        }
    }

    private bool CanSummarize() => _summary is not null && _emails.Count > 0;

    /// <inheritdoc />
    public void FocusElement(string elementId)
    {
        ArgumentNullException.ThrowIfNull(elementId);

        _pendingFocusId = elementId;

        // Notifications point at Favorites messages: switching lists applies the focus once it has loaded.
        if (HasLoaded && SelectedFolder != _favorites)
        {
            SelectedFolder = _favorites;
        }
        else if (HasLoaded)
        {
            ApplyPendingFocus();
        }
    }

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        await LoadFoldersAsync(ct);
        await LoadMessagesAsync(ct);
    }

    // The tree is a navigation aid: if it cannot be read, Favorites still lists (signing out is not ignored).
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        IReadOnlyList<MailFolderNode> nodes;
        try
        {
            nodes = await _email!.GetFoldersAsync(ct);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or GraphNotConnectedException))
        {
            return;
        }

        var expanded = Folders.SelectMany(f => f.SelfAndDescendants()).Where(f => f.IsExpanded).Select(f => f.Id).ToHashSet();
        var selectedId = SelectedFolder?.Id;

        _isRebuildingFolders = true;
        try
        {
            Folders.Clear();
            var showFavorites = _settings?.GetEmail().ShowFavorites ?? false;
            if (showFavorites)
            {
                Folders.Add(_favorites);
            }

            foreach (var node in nodes)
            {
                Folders.Add(MailFolderViewModel.From(node));
            }

            var all = Folders.SelectMany(f => f.SelfAndDescendants()).ToList();
            foreach (var folder in all.Where(f => expanded.Contains(f.Id)))
            {
                folder.IsExpanded = true;
            }

            // Hidden Favorites: land on the first folder (the inbox); Favorites stays the fallback for an empty tree.
            SelectedFolder = all.FirstOrDefault(f => f.Id == selectedId)
                ?? (showFavorites ? _favorites : Folders.FirstOrDefault() ?? _favorites);
        }
        finally
        {
            _isRebuildingFolders = false;
        }
    }

    private async Task LoadMessagesAsync(CancellationToken ct)
    {
        var emails = SelectedFolder?.Id is { } folderId
            ? await _email!.GetFolderEmailsAsync(folderId, ct)
            : await _email!.GetRecentEmailsAsync(ct);
        _emails = emails;
        SummarizeCommand.NotifyCanExecuteChanged();

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

    partial void OnSelectedFolderChanged(MailFolderViewModel? value)
    {
        if (value is not null && HasLoaded && !_isRebuildingFolders)
        {
            LoadFolderCommand.Execute(null);
        }
    }

    partial void OnSelectedItemChanged(EmailRowViewModel? value)
    {
        var request = ++_bodyRequest;
        SelectedHtml = "";
        AreRemoteImagesAllowed = false;
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
            var html = await _email!.GetEmailHtmlAsync(emailId);
            if (request == _bodyRequest)
            {
                SelectedHtml = html;
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
