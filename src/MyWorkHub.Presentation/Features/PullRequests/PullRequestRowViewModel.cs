using System.Globalization;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Presentation.Features.PullRequests;

/// <summary>One row of the pull request list (immutable; a refresh replaces the rows).</summary>
public sealed class PullRequestRowViewModel
{
    private const string CREATED_FORMAT = "d MMM yyyy";

    public PullRequestRowViewModel(PullRequestItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Id = item.Id;
        Title = item.Title;
        Author = item.Author;
        Url = item.Url;
        IsDraft = item.IsDraft;
        IsAuthor = item.Role == PullRequestRole.AUTHOR;
        IdText = "!" + item.Id.ToString(CultureInfo.InvariantCulture);
        RepositoryText = $"{item.Project} / {item.Repository}";
        BranchesText = $"{item.SourceBranch} → {item.TargetBranch}";
        CreatedAtText = item.CreatedAt.ToLocalTime().ToString(CREATED_FORMAT, CultureInfo.CurrentCulture);
        RoleText = IsAuthor ? "Authored" : "Reviewing";
        VoteText = IsAuthor ? "" : Describe(item.MyVote);
    }

    public int Id { get; }

    public string Title { get; }

    public string Author { get; }

    public string Url { get; }

    public bool IsDraft { get; }

    /// <summary>True for the user's own pull requests, false for ones they review.</summary>
    public bool IsAuthor { get; }

    /// <summary>Azure DevOps' own short form, e.g. <c>!1234</c>.</summary>
    public string IdText { get; }

    public string RepositoryText { get; }

    public string BranchesText { get; }

    /// <summary>Creation date in the user's local time zone and culture.</summary>
    public string CreatedAtText { get; }

    public string RoleText { get; }

    /// <summary>The user's vote as a reviewer; empty on their own pull requests.</summary>
    public string VoteText { get; }

    private static string Describe(PullRequestVote vote) => vote switch
    {
        PullRequestVote.APPROVED => "Approved",
        PullRequestVote.SUGGESTIONS => "Approved with suggestions",
        PullRequestVote.WAITING => "Waiting for author",
        PullRequestVote.REJECTED => "Rejected",
        _ => "No vote yet",
    };
}
