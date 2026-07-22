using MyWorkHub.Core.Models;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Projects a Graph <see cref="Chat"/> onto the <see cref="TeamsChatItem"/> read model and
/// decides whether a chat is unread.
///
/// NOTE: the Graph <c>chat</c> resource has no <c>unreadMessageCount</c> property (the field
/// named in ARCHITECTURE.md §6/§12 does not exist in Microsoft Graph). "Unread" is therefore
/// derived from <c>viewpoint.lastMessageReadDateTime</c> vs <c>lastMessagePreview.createdDateTime</c>,
/// and the per-chat <see cref="TeamsChatItem.UnreadCount"/> is reported as 1 (one unread
/// conversation) since Graph exposes no per-chat message tally. See T075/T076 notes in TASKS.md.
/// </summary>
public static class GraphTeamsMapper
{
    /// <summary>
    /// True when the chat has a last message the user has not yet read — i.e. the last message
    /// is newer than the user's last-read marker, or the user has never read the chat.
    /// </summary>
    public static bool IsUnread(Chat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        var lastMessageAt = chat.LastMessagePreview?.CreatedDateTime;
        if (lastMessageAt is null)
        {
            return false;
        }

        var lastReadAt = chat.Viewpoint?.LastMessageReadDateTime;
        return lastReadAt is null || lastMessageAt > lastReadAt;
    }

    /// <summary>Projects a chat (expanded with its <c>lastMessagePreview</c>) onto the digest read model.</summary>
    public static TeamsChatItem ToTeamsChatItem(Chat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        var preview = chat.LastMessagePreview;
        var sender = preview?.From?.User?.DisplayName
            ?? preview?.From?.Application?.DisplayName
            ?? "(unknown sender)";

        // 1:1 chats carry no topic; fall back to the last sender's name so the row is identifiable.
        var name = chat.Topic is { Length: > 0 } topic
            ? topic
            : preview?.From?.User?.DisplayName is { Length: > 0 } senderName ? senderName : "(group chat)";

        return new TeamsChatItem(
            ChatId: chat.Id ?? "",
            ChatName: name,
            LastSenderName: sender,
            MessagePreview: TeamsMessagePreview.ToPlainText(preview?.Body?.Content, preview?.Body?.ContentType),
            ReceivedAt: preview?.CreatedDateTime?.UtcDateTime ?? default,
            UnreadCount: 1,
            DeepLinkUrl: chat.WebUrl ?? "");
    }
}
