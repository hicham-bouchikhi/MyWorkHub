namespace MyWorkHub.Core.Models;

/// <summary>An Azure DevOps pull request awaiting the current user's review.</summary>
public record PullRequestItem(
    int Id,
    string Title,
    string Author,
    string Repository,
    DateTime CreatedAt,
    string VoteStatus,
    string Url,
    string? ReviewerTeam = null)
{
    /// <summary>The AzDO team project the repository belongs to.</summary>
    public string ProjectName { get; init; } = "";

    /// <summary>Source branch short name (with the <c>refs/heads/</c> prefix stripped).</summary>
    public string SourceBranch { get; init; } = "";

    /// <summary>Target branch short name (with the <c>refs/heads/</c> prefix stripped).</summary>
    public string TargetBranch { get; init; } = "";

    /// <summary>HTTPS clone URL of the repository (unauthenticated).</summary>
    public string CloneUrl { get; init; } = "";
}
