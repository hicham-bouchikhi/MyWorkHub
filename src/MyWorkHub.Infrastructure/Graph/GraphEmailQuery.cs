namespace MyWorkHub.Infrastructure.Graph;

/// <summary>OData query parameters for the "important emails" digest.</summary>
public static class GraphEmailQuery
{
    /// <summary>Unread OR flagged messages.</summary>
    public static string ImportantMessagesFilter { get; } = "isRead eq false or flag/flagStatus eq 'flagged'";

    /// <summary>Maximum number of messages to pull for the digest.</summary>
    public static int DefaultTop { get; } = 25;

    /// <summary>Fields projected from Graph (keeps the payload small).</summary>
    public static IReadOnlyList<string> SelectFields { get; } =
    [
        "id", "subject", "bodyPreview", "receivedDateTime", "isRead", "flag", "from",
    ];

    /// <summary>
    /// The mail folders to read for the digest and unread counter: the user's configured
    /// watched folders, or the Inbox alone when nothing has been configured yet.
    /// </summary>
    public static IReadOnlyList<string> WatchedFolderIds(IReadOnlyList<string> configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        return configured.Count > 0 ? configured : ["inbox"];
    }
}
