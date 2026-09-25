namespace MyWorkHub.Core.Features.Teams;

/// <summary>One Teams chat with a preview of its latest message.</summary>
/// <param name="Id">Graph chat id.</param>
/// <param name="Title">Chat topic, or the latest sender's name for untitled (one-on-one) chats.</param>
/// <param name="LastMessagePreview">Plain-text excerpt of the latest message; empty when none.</param>
/// <param name="LastSenderName">Display name of the latest message's sender; empty when unknown.</param>
/// <param name="LastMessageAt">When the latest message was sent (UTC), if any.</param>
/// <param name="IsUnread">Whether the latest message arrived after the user last read the chat.</param>
/// <param name="WebUrl">Link that opens the chat in Teams; empty when the server gave none.</param>
public sealed record TeamsChatItem(
    string Id,
    string Title,
    string LastMessagePreview,
    string LastSenderName,
    DateTime? LastMessageAt,
    bool IsUnread,
    string WebUrl);
