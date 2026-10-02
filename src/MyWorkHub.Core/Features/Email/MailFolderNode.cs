namespace MyWorkHub.Core.Features.Email;

/// <summary>One mail folder of the user's mailbox, with its sub-folders.</summary>
/// <param name="Id">Graph mail-folder id.</param>
/// <param name="DisplayName">Folder name as shown in Outlook.</param>
/// <param name="UnreadCount">Unread messages directly in this folder.</param>
/// <param name="Children">Sub-folders, in the server's order.</param>
public sealed record MailFolderNode(
    string Id,
    string DisplayName,
    int UnreadCount,
    IReadOnlyList<MailFolderNode> Children);
