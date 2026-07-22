namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>WIQL queries for the work-item views (T020).</summary>
public static class WorkItemWiql
{
    /// <summary>
    /// Open work items in the current sprint assigned to the signed-in user.
    /// <c>@Me</c> and <c>@CurrentIteration</c> are resolved server-side by Azure DevOps.
    /// </summary>
    public static string CurrentSprintAssignedToMe { get; } =
        "SELECT [System.Id] FROM WorkItems " +
        "WHERE [System.AssignedTo] = @Me " +
        "AND [System.IterationPath] = @CurrentIteration " +
        "AND [System.State] <> 'Closed' AND [System.State] <> 'Removed' " +
        "ORDER BY [System.ChangedDate] DESC";
}
