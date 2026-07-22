using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.UI.Platform;

/// <summary>
/// <see cref="IFolderPicker"/> over Avalonia's cross-platform <c>IStorageProvider</c>. Uses the
/// desktop main window as the owning <c>TopLevel</c>. Returns the selected local folder path,
/// or <c>null</c> when the user cancels, no window/picker is available, or the chosen folder
/// has no local filesystem path (e.g. a virtual location).
/// </summary>
public sealed class AvaloniaFolderPicker : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string title, string? startPath = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var provider = window.StorageProvider;
        if (provider is not { CanPickFolder: true })
        {
            return null;
        }

        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startPath))
        {
            start = await provider.TryGetFolderFromPathAsync(startPath).ConfigureAwait(true);
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        }).ConfigureAwait(true);

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
