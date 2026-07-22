using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.Navigation;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

/// <summary>In-memory <see cref="ICredentialStore"/> for view-model tests.</summary>
internal sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public void Save(string key, string plaintext) => _values[key] = plaintext;

    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;

    public void Delete(string key) => _values.Remove(key);
}

/// <summary>Records the last opened URL.</summary>
internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public string? LastUrl { get; private set; }

    public void Open(string url) => LastUrl = url;
}

/// <summary>Configurable <see cref="IAzureDevOpsService"/> double.</summary>
internal sealed class FakeAzureDevOpsService : IAzureDevOpsService
{
    private readonly IReadOnlyList<PullRequestItem> _pullRequests;
    private readonly IReadOnlyList<WorkItem> _workItems;
    private readonly IReadOnlyList<WorkItemMention> _mentions;
    private readonly WorkItemDetails? _workItemDetails;
    private readonly bool _throw;
    private readonly bool _throwMentions;
    private readonly bool _throwOnDetails;

    public FakeAzureDevOpsService(
        IReadOnlyList<PullRequestItem>? pullRequests = null,
        IReadOnlyList<WorkItem>? workItems = null,
        bool throwOnCall = false,
        IReadOnlyList<WorkItemMention>? mentions = null,
        bool throwOnMentions = false,
        WorkItemDetails? workItemDetails = null,
        bool throwOnDetails = false)
    {
        _pullRequests = pullRequests ?? [];
        _workItems = workItems ?? [];
        _mentions = mentions ?? [];
        _workItemDetails = workItemDetails;
        _throw = throwOnCall;
        _throwMentions = throwOnMentions;
        _throwOnDetails = throwOnDetails;
    }

    public Task<IReadOnlyList<PullRequestItem>> GetPullRequestsForReviewAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_pullRequests);

    public Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_workItems);

    public Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(IReadOnlyList<WorkItem> workItems, CancellationToken ct = default) =>
        _throwMentions ? throw new HttpRequestException("mentions boom") : Task.FromResult(_mentions);

    public Task<WorkItemDetails?> GetWorkItemDetailsAsync(int id, CancellationToken ct = default) =>
        _throwOnDetails
            ? throw new HttpRequestException("details boom")
            : Task.FromResult(_workItemDetails);

    public Task UpdateWorkItemAsync(int id, string? description, string? acceptanceCriteria, CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.CompletedTask;
}

/// <summary>In-memory <see cref="ISeenMentionRepository"/> for ViewModel tests.</summary>
internal sealed class FakeSeenMentionRepository : ISeenMentionRepository
{
    private readonly HashSet<int> _seen = [];

    public int MarkSeenCallCount { get; private set; }
    public int? LastMarkedSeenId { get; private set; }

    public FakeSeenMentionRepository(IEnumerable<int>? initialSeenIds = null)
    {
        if (initialSeenIds is not null)
        {
            foreach (var id in initialSeenIds)
            {
                _seen.Add(id);
            }
        }
    }

    public Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlySet<int>>(_seen);

    public Task MarkSeenAsync(int commentId, CancellationToken ct = default)
    {
        MarkSeenCallCount++;
        LastMarkedSeenId = commentId;
        _seen.Add(commentId);
        return Task.CompletedTask;
    }
}

/// <summary>Controllable <see cref="IGraphConnectionService"/> for SettingsViewModel tests.</summary>
internal sealed class FakeGraphConnectionService : IGraphConnectionService
{
    private string? _account;
    private readonly bool _connectSucceeds;
    private readonly string? _connectError;

    public FakeGraphConnectionService(
        string? initialAccount = null,
        bool connectSucceeds = true,
        string? connectError = null)
    {
        _account = initialAccount;
        _connectSucceeds = connectSucceeds;
        _connectError = connectError;
    }

    public bool ConnectCalled { get; private set; }
    public bool DisconnectCalled { get; private set; }

    public Task<GraphConnectionResult> ConnectAsync(CancellationToken ct = default)
    {
        ConnectCalled = true;
        if (_connectSucceeds)
        {
            _account = "user@contoso.com";
            return Task.FromResult(new GraphConnectionResult(true, _account, null));
        }

        return Task.FromResult(new GraphConnectionResult(false, null, _connectError ?? "error"));
    }

    public Task DisconnectAsync()
    {
        DisconnectCalled = true;
        _account = null;
        return Task.CompletedTask;
    }

    public Task<string?> GetConnectedAccountAsync() => Task.FromResult(_account);
}

/// <summary>Stateful in-memory <see cref="ITodoRepository"/> for TodoViewModel tests.</summary>
internal sealed class FakeTodoRepository : ITodoRepository
{
    private readonly List<TodoItem> _items;
    private readonly bool _throw;

    public FakeTodoRepository(IEnumerable<TodoItem>? seed = null, bool throwOnCall = false)
    {
        _items = seed?.ToList() ?? [];
        _throw = throwOnCall;
    }

    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default) =>
        _throw ? throw new InvalidOperationException("boom")
               : Task.FromResult<IReadOnlyList<TodoItem>>(_items.OrderByDescending(t => t.CreatedAt).ToList());

    public Task AddAsync(TodoItem item, CancellationToken ct = default)
    {
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TodoItem item, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _items.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}

/// <summary>Configurable <see cref="IEmailService"/> double.</summary>
internal sealed class FakeEmailService : IEmailService
{
    private readonly IReadOnlyList<EmailItem> _emails;
    private readonly IReadOnlyList<MailFolder> _folders;
    private readonly bool _throw;

    public FakeEmailService(
        IReadOnlyList<EmailItem>? emails = null,
        bool throwOnCall = false,
        IReadOnlyList<MailFolder>? folders = null)
    {
        _emails = emails ?? [];
        _folders = folders ?? [];
        _throw = throwOnCall;
    }

    public int GetBodyCallCount { get; private set; }

    public Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_emails);

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_emails.Count);

    public Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_folders);

    public Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default)
    {
        GetBodyCallCount++;
        return _throw
            ? throw new HttpRequestException("boom")
            : Task.FromResult($"Full body of message {id}");
    }

    public Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default) =>
        Task.CompletedTask;
}

/// <summary>Configurable <see cref="IEmailSummaryService"/> double.</summary>
internal sealed class FakeEmailSummaryService : IEmailSummaryService
{
    private readonly string _summary;
    private readonly bool _throw;

    public FakeEmailSummaryService(string summary = "• A short digest.", bool throwOnCall = false)
    {
        _summary = summary;
        _throw = throwOnCall;
    }

    public int CallCount { get; private set; }

    public IReadOnlyList<EmailItem>? LastEmails { get; private set; }

    public Task<string> SummarizeAsync(IReadOnlyList<EmailItem> emails, CancellationToken ct = default)
    {
        CallCount++;
        LastEmails = emails;
        return _throw
            ? throw new InvalidOperationException("The Claude CLI is not available.")
            : Task.FromResult(_summary);
    }
}

/// <summary>Captures the folder ids saved from the Settings email tab.</summary>
internal sealed class FakeEmailSettingsService : IEmailSettingsService
{
    private List<string> _folderIds;

    public FakeEmailSettingsService(IEnumerable<string>? folderIds = null) =>
        _folderIds = folderIds?.ToList() ?? ["inbox"];

    public IReadOnlyList<string>? SavedFolderIds { get; private set; }

    public IReadOnlyList<string> GetWatchedFolderIds() => _folderIds;

    public void Save(IReadOnlyList<string> folderIds)
    {
        SavedFolderIds = folderIds.ToList();
        _folderIds = folderIds.ToList();
    }
}

/// <summary>Configurable <see cref="ITeamsService"/> double.</summary>
internal sealed class FakeTeamsService : ITeamsService
{
    private readonly IReadOnlyList<TeamsChatItem> _chats;
    private readonly bool _throw;

    public FakeTeamsService(IReadOnlyList<TeamsChatItem>? chats = null, bool throwOnCall = false)
    {
        _chats = chats ?? [];
        _throw = throwOnCall;
    }

    public Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_chats);

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
        _throw ? throw new HttpRequestException("boom") : Task.FromResult(_chats.Sum(c => c.UnreadCount));
}

/// <summary>Records the last toast raised.</summary>
internal sealed class FakeNotificationService : INotificationService
{
    public string? LastTitle { get; private set; }

    public string? LastMessage { get; private set; }

    public NotificationSeverity? LastSeverity { get; private set; }

    public int NotifyCount { get; private set; }

    /// <summary>The activation callback passed with the last notification, if any.</summary>
    public Action? LastOnActivated { get; private set; }

    public void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.INFORMATION, Action? onActivated = null)
    {
        LastTitle = title;
        LastMessage = message;
        LastSeverity = severity;
        LastOnActivated = onActivated;
        NotifyCount++;
    }
}

/// <summary>Records navigation requests for view-model tests.</summary>
internal sealed class FakeNavigationService : INavigationService
{
    public Type? LastNavigatedTo { get; private set; }

    public ViewModelBase? CurrentPage { get; private set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(Type viewModelType)
    {
        LastNavigatedTo = viewModelType;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CurrentPage)));
    }
}

/// <summary>Configurable <see cref="IPrReviewService"/> double.</summary>
internal sealed class FakePrReviewService : IPrReviewService
{
    private readonly PrerequisiteCheckResult _prerequisites;
    private readonly string _htmlPath;
    private readonly bool _throw;

    public FakePrReviewService(
        PrerequisiteCheckResult? prerequisites = null,
        string htmlPath = @"C:\reviews\report.html",
        bool throwOnReview = false)
    {
        _prerequisites = prerequisites ?? new PrerequisiteCheckResult(true, true, null);
        _htmlPath = htmlPath;
        _throw = throwOnReview;
    }

    public bool ReviewCalled { get; private set; }

    public Task<PrerequisiteCheckResult> CheckPrerequisitesAsync() => Task.FromResult(_prerequisites);

    public Task<string> ReviewAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ReviewCalled = true;
        progress?.Report("fake step");
        return _throw ? throw new InvalidOperationException("boom") : Task.FromResult(_htmlPath);
    }
}

/// <summary>Returns a canned folder path (or null to simulate cancel).</summary>
internal sealed class FakeFolderPicker : IFolderPicker
{
    private readonly string? _result;

    public FakeFolderPicker(string? result = null) => _result = result;

    public string? LastStartPath { get; private set; }

    public Task<string?> PickFolderAsync(string title, string? startPath = null)
    {
        LastStartPath = startPath;
        return Task.FromResult(_result);
    }
}

/// <summary>Blocks inside <see cref="ReviewAsync"/> until its token is cancelled.</summary>
internal sealed class CancellingPrReviewService : IPrReviewService
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the review has begun and is waiting to be cancelled.</summary>
    public Task Started => _started.Task;

    public Task<PrerequisiteCheckResult> CheckPrerequisitesAsync() =>
        Task.FromResult(new PrerequisiteCheckResult(true, true, null));

    public async Task<string> ReviewAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        _started.TrySetResult();
        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false); // cancellation surfaces as OperationCanceledException
        return "";
    }
}

/// <summary>Captures the last saved settings.</summary>
internal sealed class FakeAzureDevOpsSettingsService : IAzureDevOpsSettingsService
{
    private string _organizationUrl;
    private List<string> _projects;
    private bool _hasPat;

    public FakeAzureDevOpsSettingsService(string organizationUrl = "https://dev.azure.com/cegid/", IEnumerable<string>? projects = null, bool hasPat = false)
    {
        _organizationUrl = organizationUrl;
        _projects = projects?.ToList() ?? [];
        _hasPat = hasPat;
    }

    public string? SavedPat { get; private set; }

    public bool SaveCalled { get; private set; }

    public AzureDevOpsSettings Get() => new(_organizationUrl, _projects, _hasPat);

    public void Save(string organizationUrl, IEnumerable<string> projects, string? personalAccessToken)
    {
        SaveCalled = true;
        _organizationUrl = organizationUrl;
        _projects = projects.ToList();
        if (personalAccessToken is not null)
        {
            SavedPat = personalAccessToken;
            _hasPat = !string.IsNullOrWhiteSpace(personalAccessToken);
        }
    }
}
