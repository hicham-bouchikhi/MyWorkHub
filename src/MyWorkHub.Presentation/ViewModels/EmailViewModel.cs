using System.Collections.ObjectModel;
using System.Globalization;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Email digest (Phase 5, T032–T035): loads the user's important (unread/flagged) mail
/// from <see cref="IEmailService"/>, supports a client-side sender/subject filter, opens a
/// selected message in Outlook on the web, and produces a per-message AI summary.
/// </summary>
public sealed partial class EmailViewModel : PageViewModel
{
    private readonly IEmailService? _emailService;
    private readonly IBrowserLauncher? _launcher;
    private readonly ILogger<EmailViewModel>? _logger;
    private readonly IEmailSummaryService? _summaryService;
    private readonly INotificationService? _notification;
    private readonly INavigationService? _navigation;
    private readonly NotificationCenterViewModel? _notificationCenter;

    /// <summary>Unfiltered master list; <see cref="Emails"/> is the filtered view of this.</summary>
    private readonly List<EmailRow> _allEmails = [];

    /// <summary>
    /// Summaries kept by message id so they survive a refresh (this view re-fetches on every
    /// navigation, rebuilding the rows) — the summary re-appears when the user returns.
    /// </summary>
    private readonly Dictionary<string, string> _summaries = new(StringComparer.Ordinal);

    /// <summary>Full bodies kept by message id so a second Summarise click does not re-fetch from Graph.</summary>
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _searchText;

    public EmailViewModel(
        IEmailService? emailService = null,
        IBrowserLauncher? launcher = null,
        ILogger<EmailViewModel>? logger = null,
        IEmailSummaryService? summaryService = null,
        INotificationService? notification = null,
        INavigationService? navigation = null,
        NotificationCenterViewModel? notificationCenter = null)
        : base("Email")
    {
        _emailService = emailService;
        _launcher = launcher;
        _logger = logger;
        _summaryService = summaryService;
        _notification = notification;
        _navigation = navigation;
        _notificationCenter = notificationCenter;
    }

    /// <summary>True when the AI summary feature is available (a summary service is configured).</summary>
    public bool CanSummarize => _summaryService is not null;

    /// <summary>Currently displayed rows, after the search filter is applied.</summary>
    public ObservableCollection<EmailRow> Emails { get; } = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        ErrorMessage = null;
        IsEmpty = false;

        if (_emailService is null)
        {
            _allEmails.Clear();
            ApplyFilter();
            return;
        }

        IsLoading = true;
        try
        {
            var items = await _emailService.GetImportantEmailsAsync().ConfigureAwait(true);

            _allEmails.Clear();
            foreach (var item in items)
            {
                var row = new EmailRow(item);
                if (_summaries.TryGetValue(row.Id, out var cached))
                {
                    row.SummaryText = cached; // restore a summary produced before this refresh
                }

                if (_bodies.TryGetValue(row.Id, out var cachedBody))
                {
                    row.FullBody = cachedBody;
                }

                _allEmails.Add(row);
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            LogLoadFailure(ex);
            _allEmails.Clear();
            Emails.Clear();
            ErrorMessage = "Couldn't load your email. Make sure Microsoft 365 is connected in Settings.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Summarises a single message and surfaces the result through the notification system.
    /// Concurrent execution is allowed so summarising one row does not disable every other
    /// row's button (all rows bind to this one command instance); each row's own
    /// <see cref="EmailRow.IsSummarizing"/> guards against double-summarising the same message.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SummarizeRowAsync(EmailRow? row)
    {
        if (row is null || _summaryService is null || row.IsSummarizing)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        row.BeginSummary(cts);
        var operation = _notificationCenter?.BeginActivity($"Summarising \"{row.Subject}\"", row.CancelSummary);
        try
        {
            if (row.FullBody is null && _emailService is not null)
            {
                try
                {
                    var fetchedBody = await _emailService
                        .GetEmailBodyAsync(row.Id, cts.Token)
                        .ConfigureAwait(true);
                    row.FullBody = fetchedBody;
                    _bodies[row.Id] = fetchedBody;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogBodyFetchFailure(ex);
                }
            }

            var enriched = row.Source with { FullBody = row.FullBody };
            var summary = await _summaryService.SummarizeAsync([enriched], cts.Token).ConfigureAwait(true);
            row.SummaryText = summary;
            _summaries[row.Id] = summary; // survive a later refresh / navigation

            _notification?.Notify(
                "Email summary",
                $"Summary ready for \"{row.Subject}\".",
                NotificationSeverity.SUCCESS,
                onActivated: ShowEmailPage);
        }
        catch (OperationCanceledException)
        {
            row.SummaryText = null;
            _notification?.Notify(
                "Email summary",
                $"Summary cancelled for \"{row.Subject}\".",
                NotificationSeverity.WARNING,
                onActivated: ShowEmailPage);
        }
        catch (Exception ex)
        {
            LogSummaryFailure(ex);
            row.SummaryText = null;
            _notification?.Notify(
                "Email summary",
                "Couldn't summarise this email. The AI summary uses the Claude CLI — make sure it's installed and logged in (same as code review).",
                NotificationSeverity.ERROR,
                onActivated: ShowEmailPage);
        }
        finally
        {
            if (operation is not null)
            {
                _notificationCenter?.EndActivity(operation);
            }

            row.EndSummary();
        }
    }

    /// <summary>Brings the Email page back into view (used as the notification click action).</summary>
    private void ShowEmailPage() => _navigation?.NavigateTo(typeof(EmailViewModel));

    [RelayCommand]
    private void Open(EmailRow? row)
    {
        if (row is not null)
        {
            _launcher?.Open("https://outlook.office.com/mail/id/" + row.Id);
        }
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    /// <summary>Re-projects <see cref="_allEmails"/> into <see cref="Emails"/> using the current search text.</summary>
    private void ApplyFilter()
    {
        var search = SearchText;
        IEnumerable<EmailRow> filtered = _allEmails;

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = _allEmails.Where(row =>
                row.From.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Subject.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Emails.Clear();
        foreach (var row in filtered)
        {
            Emails.Add(row);
        }

        IsEmpty = Emails.Count == 0;
    }

    private void LogLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogLoadFailureCore(_logger, exception);
        }
    }

    private void LogSummaryFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogSummaryFailureCore(_logger, exception);
        }
    }

    private void LogBodyFetchFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogBodyFetchFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load the email digest.")]
    private static partial void LogLoadFailureCore(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to summarise an email.")]
    private static partial void LogSummaryFailureCore(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch the full body for email summarisation; falling back to preview.")]
    private static partial void LogBodyFetchFailureCore(ILogger logger, Exception exception);
}

/// <summary>A single inbox message projected for the digest list; carries its own AI-summary state.</summary>
public sealed partial class EmailRow : ObservableObject
{
    private CancellationTokenSource? _cts;

    public EmailRow(EmailItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Source = item;
        Id = item.Id;
        From = item.From;
        Subject = item.Subject;
        Preview = item.Preview;
        ReceivedAt = item.ReceivedAt.ToString("dd MMM HH:mm", CultureInfo.CurrentCulture);
        IsFlagged = item.IsFlagged;
        FolderName = item.FolderName;
    }

    /// <summary>The underlying message, needed to build the AI summary.</summary>
    public EmailItem Source { get; }

    public string Id { get; }

    public string From { get; }

    public string Subject { get; }

    public string Preview { get; }

    public string ReceivedAt { get; }

    public bool IsFlagged { get; }

    public string FolderName { get; }

    /// <summary>Full plain-text body, populated lazily when Summarise is first clicked. Not observable — the UI never binds to it.</summary>
    public string? FullBody { get; set; }

    /// <summary>True while this row's summary is being generated.</summary>
    [ObservableProperty]
    private bool _isSummarizing;

    /// <summary>The AI summary for this message, once produced.</summary>
    [ObservableProperty]
    private string? _summaryText;

    /// <summary>Starts a summary: stores the token source used by <see cref="CancelSummary"/>.</summary>
    public void BeginSummary(CancellationTokenSource cts)
    {
        _cts = cts;
        IsSummarizing = true;
    }

    /// <summary>Requests cancellation of the in-flight summary (used by the bell's Stop button).</summary>
    public void CancelSummary()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already completed; nothing to cancel.
        }
    }

    /// <summary>Ends the summary (any outcome): clears the busy flag and drops the token source.</summary>
    public void EndSummary()
    {
        IsSummarizing = false;
        _cts = null;
    }
}
