using System.Collections.ObjectModel;
using MyWorkHub.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// PR review queue (Phase 6, T036–T040): loads the current user's review queue from
/// <see cref="IAzureDevOpsService"/>, oldest first. If no PAT is configured it shows a
/// friendly "configure in Settings" message instead of failing.
/// </summary>
public sealed partial class PullRequestsViewModel : PageViewModel
{
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly ICredentialStore? _credentials;
    private readonly IAzureDevOpsSettingsService? _settings;
    private readonly IBrowserLauncher? _launcher;
    private readonly IPrReviewService? _reviewService;
    private readonly INotificationService? _notification;
    private readonly NotificationCenterViewModel? _notificationCenter;
    private readonly ILogger<PullRequestsViewModel>? _logger;

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

    public PullRequestsViewModel(
        IAzureDevOpsService? azureDevOps = null,
        ICredentialStore? credentials = null,
        IBrowserLauncher? launcher = null,
        ILogger<PullRequestsViewModel>? logger = null,
        IAzureDevOpsSettingsService? settings = null,
        IPrReviewService? reviewService = null,
        INotificationService? notification = null,
        NotificationCenterViewModel? notificationCenter = null)
        : base("Pull Requests")
    {
        _azureDevOps = azureDevOps;
        _credentials = credentials;
        _launcher = launcher;
        _logger = logger;
        _settings = settings;
        _reviewService = reviewService;
        _notification = notification;
        _notificationCenter = notificationCenter;
    }

    /// <summary>Review queue rows, oldest first.</summary>
    public ObservableCollection<PullRequestRow> PullRequests { get; } = [];

    private bool _hasLoaded;

    /// <summary>
    /// Loads the queue the first time the page is shown. On later visits it does nothing, so a
    /// cached page returned to mid-review keeps its rows (and their in-progress spinners) intact.
    /// The manual Refresh command still forces a reload.
    /// </summary>
    [RelayCommand]
    private async Task EnsureLoadedAsync()
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _hasLoaded = true;
        ErrorMessage = null;
        NeedsConfiguration = false;
        IsEmpty = false;

        if (_credentials is null || string.IsNullOrWhiteSpace(_credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT)))
        {
            PullRequests.Clear();
            ConfigurationMessage = "Configure your Azure DevOps PAT in Settings → Integrations.";
            NeedsConfiguration = true;
            return;
        }

        if (_settings is { } settings && settings.Get().Projects.Count == 0)
        {
            PullRequests.Clear();
            ConfigurationMessage = "Add at least one Azure DevOps project in Settings → Integrations.";
            NeedsConfiguration = true;
            return;
        }

        if (_azureDevOps is null)
        {
            PullRequests.Clear();
            IsEmpty = true;
            return;
        }

        IsLoading = true;
        try
        {
            var items = await _azureDevOps.GetPullRequestsForReviewAsync().ConfigureAwait(true);
            var rows = items
                .Select(PullRequestRow.From)
                .OrderByDescending(static r => r.AgeDays)
                .ToList();

            PullRequests.Clear();
            foreach (var row in rows)
            {
                PullRequests.Add(row);
            }

            IsEmpty = PullRequests.Count == 0;
        }
        catch (Exception ex)
        {
            LogLoadFailure(ex);
            PullRequests.Clear();
            ErrorMessage = "Couldn't load pull requests from Azure DevOps. Check your PAT and projects in Settings.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Open(PullRequestRow? row)
    {
        if (row is not null)
        {
            _launcher?.Open(row.Url);
        }
    }

    [RelayCommand]
    private async Task ReviewPrAsync(PullRequestRow? row)
    {
        if (row is null || _reviewService is null || row.IsReviewing)
        {
            return;
        }

        var label = $"{row.Repository} PR #{row.Source.Id}";
        using var cts = new CancellationTokenSource();
        row.BeginReview(cts);
        var operation = _notificationCenter?.BeginActivity(label, row.CancelReview);
        var progress = new Progress<string>(row.AppendReviewStep);
        try
        {
            row.AppendReviewStep("Checking prerequisites (git, claude, work folder)…");
            var check = await _reviewService.CheckPrerequisitesAsync().ConfigureAwait(true);
            if (!check.GitFound || !check.ClaudeFound || check.WorkFolderIssue is not null)
            {
                var message = BuildPrerequisiteError(check);
                row.AppendReviewStep($"Blocked: {message}");
                _notification?.Notify("Review", $"{label}: {message}", NotificationSeverity.WARNING);
                return;
            }

            var htmlPath = await _reviewService.ReviewAsync(row.Source, progress, cts.Token).ConfigureAwait(true);
            _launcher?.Open(htmlPath);
            row.AppendReviewStep("Done — report opened in browser.");
            _notification?.Notify("Review", $"{label}: review ready — opened in browser.", NotificationSeverity.SUCCESS);
        }
        catch (OperationCanceledException)
        {
            row.AppendReviewStep("Cancelled.");
            _notification?.Notify("Review", $"{label}: review cancelled.", NotificationSeverity.WARNING);
        }
        catch (Exception ex)
        {
            LogReviewFailure(ex);
            row.AppendReviewStep($"Failed: {ex.Message}");
            _notification?.Notify("Review", $"{label}: review failed — {ex.Message}", NotificationSeverity.ERROR);
        }
        finally
        {
            if (operation is not null)
            {
                _notificationCenter?.EndActivity(operation);
            }

            row.EndReview();
        }
    }

    [RelayCommand]
    private void CancelReview(PullRequestRow? row)
    {
        if (row is null)
        {
            return;
        }

        LogReviewCancelRequested(row.Source.Id);
        row.CancelReview();
    }

    private void LogReviewCancelRequested(int pullRequestId)
    {
        if (_logger is not null)
        {
            LogReviewCancelRequestedCore(_logger, pullRequestId);
        }
    }

    private static string BuildPrerequisiteError(PrerequisiteCheckResult check)
    {
        if (!check.GitFound)
        {
            return "git was not found. Install Git or set its location on PATH.";
        }

        if (!check.ClaudeFound)
        {
            return "The Claude Code CLI was not found. Install it, or set its path in Settings → Preferences → Workspace.";
        }

        return check.WorkFolderIssue ?? "The workspace is not ready for review.";
    }

    private void LogReviewFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogReviewFailureCore(_logger, exception);
        }
    }

    private void LogLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load the Azure DevOps pull request queue.")]
    private static partial void LogLoadFailureCore(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude Code PR review failed.")]
    private static partial void LogReviewFailureCore(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cancellation requested for PR #{PullRequestId} review.")]
    private static partial void LogReviewCancelRequestedCore(ILogger logger, int pullRequestId);
}
