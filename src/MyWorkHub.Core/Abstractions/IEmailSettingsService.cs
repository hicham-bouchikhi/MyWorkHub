namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Reads and persists which mail folders feed the email digest. Values go to
/// <c>appsettings.json</c>; saving also updates the live configuration so the email service
/// picks up the new folder set without an app restart. Mirrors
/// <see cref="IWorkspaceSettingsService"/>.
/// </summary>
public interface IEmailSettingsService
{
    /// <summary>The currently watched folder ids (or well-known names such as "inbox").</summary>
    IReadOnlyList<string> GetWatchedFolderIds();

    /// <summary>Persists the watched folder ids and refreshes the live configuration.</summary>
    void Save(IReadOnlyList<string> folderIds);
}
