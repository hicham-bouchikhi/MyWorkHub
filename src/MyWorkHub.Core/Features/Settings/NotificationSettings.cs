namespace MyWorkHub.Core.Features.Settings;

/// <summary>
/// How often the app looks for new items (<c>UI:RefreshIntervalMinutes</c>) and which sources notify when
/// something new arrives (<c>Notifications:*</c>).
/// </summary>
/// <param name="RefreshIntervalMinutes">Minutes between two checks, within
/// [<see cref="MinRefreshIntervalMinutes"/>, <see cref="MaxRefreshIntervalMinutes"/>].</param>
/// <param name="Emails">New unread mail in the watched folders.</param>
/// <param name="TeamsChats">New unread Teams chat messages.</param>
/// <param name="PullRequests">Pull requests the user is newly asked to review.</param>
/// <param name="WorkItems">Work items newly assigned to the user.</param>
/// <param name="Mentions">New @mentions in work item comments.</param>
/// <param name="CalendarEvents">New events in the coming week.</param>
public sealed record NotificationSettings(
    int RefreshIntervalMinutes,
    bool Emails,
    bool TeamsChats,
    bool PullRequests,
    bool WorkItems,
    bool Mentions,
    bool CalendarEvents)
{
    /// <summary>Shortest allowed refresh interval, in minutes.</summary>
    public static int MinRefreshIntervalMinutes => 1;

    /// <summary>Longest allowed refresh interval, in minutes.</summary>
    public static int MaxRefreshIntervalMinutes => 240;

    /// <summary>What a fresh install uses: every source, every five minutes.</summary>
    public static NotificationSettings Default { get; } = new(5, true, true, true, true, true, true);

    /// <summary>Keeps <see cref="RefreshIntervalMinutes"/> within bounds.</summary>
    public NotificationSettings Normalize()
        => this with { RefreshIntervalMinutes = Math.Clamp(RefreshIntervalMinutes, MinRefreshIntervalMinutes, MaxRefreshIntervalMinutes) };
}
