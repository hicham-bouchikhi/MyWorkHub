using MyWorkHub.Core.Models;

namespace MyWorkHub.Core.Abstractions;

/// <summary>Reads the user's mailbox and sends mail via Microsoft Graph.</summary>
public interface IEmailService
{
    /// <summary>Unread or flagged messages, most recent first.</summary>
    Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default);

    /// <summary>Count of unread messages in the inbox (lightweight dashboard counter).</summary>
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists the user's mail folders so they can be picked in Settings. Each folder is
    /// flagged <see cref="MailFolder.IsWatched"/> when it is in the configured digest set.
    /// </summary>
    Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default);

    /// <summary>Fetches the full plain-text body of a single message. Returns empty string when unavailable.</summary>
    Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default);

    /// <summary>Sends a mail message with an optional file attachment.</summary>
    Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default);
}
