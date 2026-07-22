using GraphFolder = Microsoft.Graph.Models.MailFolder;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>Projects a Graph mail folder onto the <see cref="Core.Models.MailFolder"/> read model.</summary>
public static class GraphMailFolderMapper
{
    /// <summary>
    /// Maps <paramref name="folder"/> (without its children), marking it watched when its
    /// opaque id is present in <paramref name="watchedIds"/>. The service resolves any
    /// well-known-name seeds (e.g. "inbox") to their opaque ids before building that set, so
    /// matching here is a plain id comparison. The service attaches nested folders separately.
    /// </summary>
    public static Core.Models.MailFolder ToMailFolder(GraphFolder folder, ISet<string> watchedIds)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(watchedIds);

        var id = folder.Id ?? "";

        return new Core.Models.MailFolder(
            Id: id,
            DisplayName: folder.DisplayName is { Length: > 0 } name ? name : "(unnamed folder)",
            UnreadItemCount: folder.UnreadItemCount ?? 0,
            IsWatched: watchedIds.Contains(id));
    }
}
