namespace MyWorkHub.Core.Features.Settings;

/// <summary>Which mail folders make up the Email page's Favorites, as stored under the <c>Email</c> section.</summary>
/// <param name="FolderIds">Graph mail-folder ids or well-known names (e.g. <c>inbox</c>); empty means the inbox.</param>
/// <param name="MaxPerFolder">How many of the newest messages to take from each folder (1–100).</param>
/// <param name="ShowFavorites">Whether the Email page shows Favorites atop its folder tree (off by default: it opens on the inbox).</param>
public sealed record EmailSettings(IReadOnlyList<string> FolderIds, int MaxPerFolder, bool ShowFavorites = false)
{
    /// <summary>Smallest allowed <see cref="MaxPerFolder"/>.</summary>
    public static int PerFolderMin => 1;

    /// <summary>Largest allowed <see cref="MaxPerFolder"/> (Graph's page size cap).</summary>
    public static int PerFolderMax => 100;

    /// <summary><see cref="MaxPerFolder"/> when unset.</summary>
    public static int PerFolderDefault => 25;
}
