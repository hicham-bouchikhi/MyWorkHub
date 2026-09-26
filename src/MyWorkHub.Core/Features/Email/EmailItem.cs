namespace MyWorkHub.Core.Features.Email;

/// <summary>One message header as shown in the mail list.</summary>
/// <param name="Id">Graph message id; also the deep-link element id of the message's row.</param>
/// <param name="From">Sender display name (or address when the name is missing).</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Preview">Short plain-text excerpt of the body, as provided by the server.</param>
/// <param name="ReceivedAt">When the message arrived (UTC).</param>
/// <param name="IsFlagged">Whether the message is flagged for follow-up.</param>
/// <param name="IsRead">Whether the message has been read.</param>
/// <param name="FolderName">Display name of the mail folder the message lives in.</param>
public sealed record EmailItem(
    string Id,
    string From,
    string Subject,
    string Preview,
    DateTime ReceivedAt,
    bool IsFlagged,
    bool IsRead,
    string FolderName);
