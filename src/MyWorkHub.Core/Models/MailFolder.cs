namespace MyWorkHub.Core.Models;

/// <summary>
/// An Outlook mail folder, projected for the folder-selection tree. <see cref="Children"/>
/// holds any nested sub-folders.
/// </summary>
public record MailFolder(
    string Id,
    string DisplayName,
    int UnreadItemCount,
    bool IsWatched)
{
    /// <summary>Nested sub-folders, or empty for a leaf folder.</summary>
    public IReadOnlyList<MailFolder> Children { get; init; } = [];
}
