namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Assigns a stable, distinct badge colour <em>index</em> to a mail-folder name. The concrete
/// colours live in the view layer (the Email view's folder-name→brush converter); this type stays
/// UI-framework-agnostic so it can be shared by any front-end and unit-tested directly.
/// </summary>
public static class FolderColorPicker
{
    /// <summary>Number of distinct badge colours the picker cycles through.</summary>
    public static int PaletteSize => 8;

    /// <summary>Returns a palette index that is stable across runs and case-insensitive.</summary>
    public static int IndexFor(string folderName)
    {
        ArgumentNullException.ThrowIfNull(folderName);

        unchecked
        {
            var hash = 17;
            foreach (var c in folderName)
            {
                hash = hash * 31 + char.ToUpperInvariant(c);
            }

            return (hash & 0x7FFFFFFF) % PaletteSize;
        }
    }
}
