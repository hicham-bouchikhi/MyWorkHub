namespace MyWorkHub.Core.Models;

/// <summary>An unread Teams conversation (DM, group chat or channel), projected for the digest.</summary>
public record TeamsChatItem(
    string ChatId,
    string ChatName,
    string LastSenderName,
    string MessagePreview,
    DateTime ReceivedAt,
    int UnreadCount,
    string DeepLinkUrl);
