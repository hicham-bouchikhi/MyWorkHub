using MyWorkHub.Core.Models;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Reads the user's unread Teams conversations via Microsoft Graph.</summary>
public interface ITeamsService
{
    /// <summary>Unread chats (DMs, group chats and — if consented — channels), most recent first.</summary>
    Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Total unread message count across the user's chats. Lightweight dashboard counter:
    /// sums <c>unreadMessageCount</c> across <c>/me/chats</c> rather than fetching previews.
    /// </summary>
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}
