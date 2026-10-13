namespace MyWorkHub.Core.Configuration;

/// <summary>Which sources raise a notification when something new arrives (configuration section <c>Notifications</c>).</summary>
public sealed class NotificationOptions
{
    public const string SECTION = "Notifications";

    public bool Emails { get; set; } = true;
    public bool TeamsChats { get; set; } = true;
    public bool PullRequests { get; set; } = true;
    public bool WorkItems { get; set; } = true;
    public bool Mentions { get; set; } = true;
    public bool CalendarEvents { get; set; } = true;
}
