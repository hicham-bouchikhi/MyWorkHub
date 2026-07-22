using System.Globalization;
using MyWorkHub.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// A pull request projected for the review-queue list. Display data is immutable; the review
/// state (spinner, live activity log, cancellation) is observable so each row can run and
/// report its own review independently while others run in parallel.
/// </summary>
public sealed partial class PullRequestRow : ObservableObject
{
    private CancellationTokenSource? _cts;

    private PullRequestRow(PullRequestItem source, int ageDays)
    {
        Source = source;
        Title = source.Title;
        Author = source.Author;
        Repository = source.Repository;
        AgeDays = ageDays;
        VoteStatus = source.VoteStatus;
        Url = source.Url;
        ReviewerTeam = source.ReviewerTeam;
    }

    /// <summary>The underlying PR, needed by the review command (branches, clone URL, project).</summary>
    public PullRequestItem Source { get; }

    // ── Review state (per row, so reviews run in parallel) ──────────────────
    [ObservableProperty]
    private bool _isReviewing;

    /// <summary>The step currently running (shown next to the spinner).</summary>
    [ObservableProperty]
    private string _reviewStatus = "";

    /// <summary>Timestamped log of every step; stays visible after the run for inspection.</summary>
    [ObservableProperty]
    private string _reviewLogText = "";

    /// <summary>True once a review has produced any log lines (controls the activity panel).</summary>
    [ObservableProperty]
    private bool _hasReviewLog;

    /// <summary>Starts a review: resets the log and stores the token source used by <see cref="CancelReview"/>.</summary>
    public void BeginReview(CancellationTokenSource cts)
    {
        _cts = cts;
        ReviewLogText = "";
        HasReviewLog = true;
        IsReviewing = true;
        AppendReviewStep("Starting review…");
    }

    /// <summary>Appends a timestamped step to the log and shows it as the current status.</summary>
    public void AppendReviewStep(string step)
    {
        var line = $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}  {step}";
        ReviewLogText = string.IsNullOrEmpty(ReviewLogText) ? line : $"{ReviewLogText}\n{line}";
        ReviewStatus = step;
    }

    /// <summary>Requests cancellation of the in-flight review.</summary>
    public void CancelReview()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already completed; nothing to cancel.
        }

        ReviewStatus = "Cancelling…";
    }

    /// <summary>Ends the review (win or lose): clears the spinner and drops the token source.</summary>
    public void EndReview()
    {
        IsReviewing = false;
        _cts = null;
    }

    public string Title { get; }

    public string Author { get; }

    public string Repository { get; }

    public int AgeDays { get; }

    public string VoteStatus { get; }

    public string Url { get; }

    /// <summary>Set when this PR is in the queue because a team (not the user directly) is a reviewer.</summary>
    public string? ReviewerTeam { get; }

    public bool IsViaTeam => !string.IsNullOrEmpty(ReviewerTeam);

    public string ViaTeamLabel => $"via {ReviewerTeam}";

    public string AgeText => AgeDays switch
    {
        <= 0 => "Today",
        1 => "1 day",
        _ => $"{AgeDays.ToString(CultureInfo.CurrentCulture)} days",
    };

    /// <summary>2–5 days old → amber highlight (T038).</summary>
    public bool IsAging => AgeDays is > 2 and <= 5;

    /// <summary>Older than 5 days → red highlight (T038).</summary>
    public bool IsStale => AgeDays > 5;

    /// <summary>
    /// Semantic badge category for the reviewer vote, used to pick a palette brush via
    /// <c>Classes.&lt;category&gt;</c> styling. One of: success | successAlt | warning | danger | neutral.
    /// </summary>
    public string BadgeCategory => VoteStatus switch
    {
        "Approved" => "success",
        "Approved with suggestions" => "successAlt",
        "Waiting for author" => "warning",
        "Rejected" => "danger",
        _ => "neutral",
    };

    public bool IsBadgeSuccess => BadgeCategory == "success";

    public bool IsBadgeSuccessAlt => BadgeCategory == "successAlt";

    public bool IsBadgeWarning => BadgeCategory == "warning";

    public bool IsBadgeDanger => BadgeCategory == "danger";

    public bool IsBadgeNeutral => BadgeCategory == "neutral";

    public static PullRequestRow From(PullRequestItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var age = (int)Math.Floor((DateTime.UtcNow - item.CreatedAt).TotalDays);
        return new PullRequestRow(item, Math.Max(0, age));
    }
}
