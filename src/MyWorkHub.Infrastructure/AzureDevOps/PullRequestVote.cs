namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>Maps Azure DevOps reviewer vote codes to display text.</summary>
public static class PullRequestVote
{
    public static string Describe(int vote) => vote switch
    {
        10 => "Approved",
        5 => "Approved with suggestions",
        -5 => "Waiting for author",
        -10 => "Rejected",
        _ => "No vote",
    };
}
