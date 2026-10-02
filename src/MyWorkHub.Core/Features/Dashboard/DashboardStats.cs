namespace MyWorkHub.Core.Features.Dashboard;

/// <summary>Summary statistics for email: unread count and recent message count.</summary>
/// <param name="UnreadCount">Number of unread emails across watched folders.</param>
public sealed record EmailStats(int UnreadCount);

/// <summary>Summary statistics for Teams: unread chat count.</summary>
/// <param name="UnreadCount">Number of chats with unread messages.</param>
public sealed record TeamsStats(int UnreadCount);

/// <summary>Summary statistics for Azure DevOps PRs and work items.</summary>
/// <param name="PullRequestCount">Total active pull requests (authored or reviewing).</param>
/// <param name="PendingReviewCount">Pull requests waiting for the user's review (vote is NONE).</param>
/// <param name="WorkItemCount">Open work items assigned to the user.</param>
public sealed record AzureDevOpsStats(int PullRequestCount, int PendingReviewCount, int WorkItemCount);

/// <summary>Summary statistics for the todo list: total items and completed count.</summary>
/// <param name="TotalCount">Total todo items.</param>
/// <param name="CompletedCount">Completed todo items.</param>
public sealed record TodoStats(int TotalCount, int CompletedCount);

/// <summary>Summary statistics for calendar: upcoming event count and next event.</summary>
/// <param name="UpcomingCount">Number of events in the next 7 days.</param>
/// <param name="NextEventTime">When the next event starts, or null if none upcoming.</param>
public sealed record CalendarStats(int UpcomingCount, DateTime? NextEventTime);
