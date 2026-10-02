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

    /// <summary>
    /// The full body of one message as an HTML fragment for the reading pane: inline (<c>cid:</c>) images are
    /// embedded as <c>data:</c> URIs and plain-text bodies are escaped into a pre-formatted block. The markup
    /// is the sender's, i.e. untrusted: it must only be rendered sandboxed (no scripts, no remote loads).
    /// </summary>
    Task<string> GetEmailHtmlAsync(string id, CancellationToken ct = default);

    /// <summary>The mailbox's folder hierarchy (hidden folders excluded), top-level folders first.</summary>
    Task<IReadOnlyList<MailFolderNode>> GetFoldersAsync(CancellationToken ct = default);

    /// <summary>
    /// The real Graph id of a folder given by id or well-known name (<c>inbox</c>, <c>sentitems</c>, …), or null
    /// when no such folder exists — lets a configured well-known name be matched against <see cref="GetFoldersAsync"/>.
    /// </summary>
    Task<string?> GetFolderIdAsync(string idOrWellKnownName, CancellationToken ct = default);

    /// <summary>The newest messages of one folder (at most <c>Email:MaxPerFolder</c>), newest first.</summary>
    Task<IReadOnlyList<EmailItem>> GetFolderEmailsAsync(string folderId, CancellationToken ct = default);
}
