using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Core.Features.Email;

/// <summary>
/// Read access to the user's mailbox. Every call throws <see cref="GraphNotConnectedException"/>
/// when the user is not signed in to Microsoft 365.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// The most recent messages of every watched folder (configuration <c>Email:FolderIds</c>, at most
    /// <c>Email:MaxPerFolder</c> each), merged newest first. A watched folder that no longer exists is
    /// skipped rather than failing the whole list.
    /// </summary>
    Task<IReadOnlyList<EmailItem>> GetRecentEmailsAsync(CancellationToken ct = default);

    /// <summary>The full body of one message as plain text (fetched on demand, not with the list).</summary>
    Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default);
}
