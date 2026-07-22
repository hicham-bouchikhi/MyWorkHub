using System.Collections.ObjectModel;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Work Items (Phase 7, T041–T044): loads the current user's sprint items from
/// <see cref="IAzureDevOpsService"/> and groups them by state (Active / New / Resolved …).
/// Shows a friendly "configure in Settings" message when no PAT is set.
/// </summary>
public sealed partial class WorkItemsViewModel : PageViewModel
{
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly ICredentialStore? _credentials;
    private readonly IAzureDevOpsSettingsService? _settings;
    private readonly IBrowserLauncher? _launcher;
    private readonly ISeenMentionRepository? _seenMentions;
    private readonly ILogger<WorkItemsViewModel>? _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _needsConfiguration;

    [ObservableProperty]
    private string _configurationMessage = "Configure your Azure DevOps PAT in Settings → Integrations.";

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNewMentions))]
    [NotifyPropertyChangedFor(nameof(NewMentionBadge))]
    private bool _hasMentions;

    // ── Detail panel ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailTitle))]
    [NotifyPropertyChangedFor(nameof(DetailTypeIcon))]
    private WorkItemRow? _selectedRow;

    [ObservableProperty]
    private bool _isDetailLoading;

    [ObservableProperty]
    private string? _detailDescription;

    [ObservableProperty]
    private string? _detailAcceptanceCriteria;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private string _editAcceptanceCriteria = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveChangesCommand))]
    private bool _isSaving;

    [ObservableProperty]
    private string? _saveError;

    public string DetailTitle => SelectedRow?.Title ?? string.Empty;

    public string DetailTypeIcon => SelectedRow?.TypeIcon ?? string.Empty;

    public bool HasNewMentions => Mentions.Any(static m => m.IsNew);

    public string NewMentionBadge
    {
        get
        {
            var count = Mentions.Count(static m => m.IsNew);
            return count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public WorkItemsViewModel(
        IAzureDevOpsService? azureDevOps = null,
        ICredentialStore? credentials = null,
        IBrowserLauncher? launcher = null,
        ILogger<WorkItemsViewModel>? logger = null,
        IAzureDevOpsSettingsService? settings = null,
        ISeenMentionRepository? seenMentions = null)
        : base("Work Items")
    {
        _azureDevOps = azureDevOps;
        _credentials = credentials;
        _launcher = launcher;
        _logger = logger;
        _settings = settings;
        _seenMentions = seenMentions;
    }

    /// <summary>Work items grouped by state, canonical order first (Active, New, Resolved).</summary>
    public ObservableCollection<WorkItemGroup> Groups { get; } = [];

    /// <summary>@mention comments on the current sprint items, newest first.</summary>
    public ObservableCollection<WorkItemMentionRow> Mentions { get; } = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        ErrorMessage = null;
        NeedsConfiguration = false;
        IsEmpty = false;

        if (_credentials is null || string.IsNullOrWhiteSpace(_credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT)))
        {
            Groups.Clear();
            ConfigurationMessage = "Configure your Azure DevOps PAT in Settings → Integrations.";
            NeedsConfiguration = true;
            return;
        }

        if (_settings is { } settings && settings.Get().Projects.Count == 0)
        {
            Groups.Clear();
            ConfigurationMessage = "Add at least one Azure DevOps project in Settings → Integrations.";
            NeedsConfiguration = true;
            return;
        }

        if (_azureDevOps is null)
        {
            Groups.Clear();
            IsEmpty = true;
            return;
        }

        IsLoading = true;
        IReadOnlyList<WorkItem> items = [];
        try
        {
            items = await _azureDevOps.GetMyWorkItemsAsync().ConfigureAwait(true);
            var groups = GroupByState(items.Select(WorkItemRow.From));

            Groups.Clear();
            foreach (var group in groups)
            {
                Groups.Add(group);
            }

            IsEmpty = Groups.Count == 0;
        }
        catch (Exception ex)
        {
            LogLoadFailure(ex);
            Groups.Clear();
            ErrorMessage = "Couldn't load work items from Azure DevOps. Check your PAT and projects in Settings.";
        }
        finally
        {
            IsLoading = false;
        }

        // Best-effort mentions: a failure here must not clear or break the work item list.
        try
        {
            var mentions = await _azureDevOps.GetWorkItemMentionsAsync(items).ConfigureAwait(true);
            IReadOnlySet<int> seenIds = _seenMentions is not null
                ? await _seenMentions.GetSeenCommentIdsAsync().ConfigureAwait(true)
                : new HashSet<int>();

            Mentions.Clear();
            foreach (var m in mentions.OrderByDescending(static m => m.CreatedAt))
            {
                Mentions.Add(WorkItemMentionRow.From(m, isNew: !seenIds.Contains(m.CommentId)));
            }

            HasMentions = Mentions.Count > 0;
            OnPropertyChanged(nameof(HasNewMentions));
            OnPropertyChanged(nameof(NewMentionBadge));
        }
        catch (Exception ex)
        {
            LogMentionLoadFailure(ex);
        }
    }

    [RelayCommand]
    private void Open(WorkItemRow? row)
    {
        if (row is not null)
        {
            _launcher?.Open(row.Url);
        }
    }

    [RelayCommand]
    private async Task OpenMentionAsync(WorkItemMentionRow? row)
    {
        if (row is null)
        {
            return;
        }

        _launcher?.Open(row.Url);
        row.MarkSeen();
        OnPropertyChanged(nameof(HasNewMentions));
        OnPropertyChanged(nameof(NewMentionBadge));

        if (_seenMentions is not null)
        {
            await _seenMentions.MarkSeenAsync(row.CommentId).ConfigureAwait(true);
        }
    }

    // ── Detail panel commands ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task ShowDetailsAsync(WorkItemRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedRow = row;
        IsEditing = false;
        SaveError = null;
        DetailDescription = null;
        DetailAcceptanceCriteria = null;

        if (_azureDevOps is null)
        {
            return;
        }

        IsDetailLoading = true;
        try
        {
            var details = await _azureDevOps.GetWorkItemDetailsAsync(row.WorkItemId).ConfigureAwait(true);
            DetailDescription = details?.Description ?? string.Empty;
            DetailAcceptanceCriteria = details?.AcceptanceCriteria ?? string.Empty;
        }
        catch (Exception ex)
        {
            LogDetailLoadFailure(ex);
            SaveError = "Couldn't load work item details.";
        }
        finally
        {
            IsDetailLoading = false;
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedRow = null;
        IsEditing = false;
        SaveError = null;
        DetailDescription = null;
        DetailAcceptanceCriteria = null;
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditDescription = DetailDescription ?? string.Empty;
        EditAcceptanceCriteria = DetailAcceptanceCriteria ?? string.Empty;
        IsEditing = true;
        SaveError = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        SaveError = null;
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private async Task SaveChangesAsync()
    {
        if (SelectedRow is null || _azureDevOps is null)
        {
            return;
        }

        IsSaving = true;
        SaveError = null;
        try
        {
            await _azureDevOps.UpdateWorkItemAsync(
                SelectedRow.WorkItemId,
                EditDescription,
                EditAcceptanceCriteria).ConfigureAwait(true);

            DetailDescription = EditDescription;
            DetailAcceptanceCriteria = EditAcceptanceCriteria;
            IsEditing = false;
        }
        catch (Exception ex)
        {
            LogSaveFailure(ex);
            SaveError = "Failed to save. Check that your PAT has write permissions.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool CanSaveChanges() => !IsSaving;

    // ── Grouping ──────────────────────────────────────────────────────────────

    private static List<WorkItemGroup> GroupByState(IEnumerable<WorkItemRow> rows) =>
        rows
            .GroupBy(static r => r.State)
            .Select(g => new WorkItemGroup(
                string.IsNullOrEmpty(g.Key) ? "Other" : g.Key,
                [.. g.OrderByDescending(static r => r.IsOverdue).ThenBy(static r => r.Title, StringComparer.CurrentCulture)]))
            .OrderBy(static g => StateRank(g.Name))
            .ThenBy(static g => g.Name, StringComparer.CurrentCulture)
            .ToList();

    private static int StateRank(string state) => state switch
    {
        "Active" => 0,
        "New" => 1,
        "Resolved" => 2,
        _ => 3,
    };

    // ── Logging ───────────────────────────────────────────────────────────────

    private void LogLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load Azure DevOps work items.")]
    private static partial void LogLoadFailureCore(ILogger logger, Exception exception);

    private void LogMentionLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogMentionLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load Azure DevOps work item mentions (non-critical).")]
    private static partial void LogMentionLoadFailureCore(ILogger logger, Exception exception);

    private void LogDetailLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogDetailLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load work item details.")]
    private static partial void LogDetailLoadFailureCore(ILogger logger, Exception exception);

    private void LogSaveFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogSaveFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to save work item changes.")]
    private static partial void LogSaveFailureCore(ILogger logger, Exception exception);
}
