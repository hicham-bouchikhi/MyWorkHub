namespace MyWorkHub.Core.Features.AzureDevOps;

/// <summary>How the user is involved in a pull request.</summary>
public enum PullRequestRole
{
    /// <summary>The user is a requested reviewer.</summary>
    REVIEWER,

    /// <summary>The user created the pull request (wins over <see cref="REVIEWER"/>).</summary>
    AUTHOR,
}

/// <summary>A reviewer vote, with Azure DevOps' own numeric codes.</summary>
public enum PullRequestVote
{
    /// <summary>Rejected.</summary>
    REJECTED = -10,

    /// <summary>Waiting for author.</summary>
    WAITING = -5,

    /// <summary>No vote yet.</summary>
    NONE = 0,

    /// <summary>Approved with suggestions.</summary>
    SUGGESTIONS = 5,

    /// <summary>Approved.</summary>
    APPROVED = 10,
}

/// <summary>An active Azure DevOps pull request the user created or is asked to review.</summary>
/// <param name="Id">Pull request id (unique across the organization); its string form is the deep-link
/// element id of the pull request's row.</param>
/// <param name="Title">Title.</param>
/// <param name="Repository">Git repository name.</param>
/// <param name="Project">Team project the repository belongs to.</param>
/// <param name="Author">Creator's display name.</param>
/// <param name="SourceBranch">Source branch short name (without <c>refs/heads/</c>).</param>
/// <param name="TargetBranch">Target branch short name (without <c>refs/heads/</c>).</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="Role">Whether the user authored it or is reviewing it.</param>
/// <param name="MyVote">The user's own reviewer vote (<see cref="PullRequestVote.NONE"/> when authoring).</param>
/// <param name="IsDraft">Whether the pull request is still a draft.</param>
/// <param name="Url">Browser URL of the pull request.</param>
public sealed record PullRequestItem(
    int Id,
    string Title,
    string Repository,
    string Project,
    string Author,
    string SourceBranch,
    string TargetBranch,
    DateTime CreatedAt,
    PullRequestRole Role,
    PullRequestVote MyVote,
    bool IsDraft,
    string Url);
