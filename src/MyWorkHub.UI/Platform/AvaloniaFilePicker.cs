using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.UI.Platform;

/// <summary>
/// <see cref="IFilePicker"/> over Avalonia's cross-platform <c>IStorageProvider</c>. Uses the
/// desktop main window as the owning <c>TopLevel</c>. Returns the selected local file path, or
/// <c>null</c> when the user cancels, no window/picker is available, or the chosen file has no
/// local filesystem path (e.g. a virtual location).
/// </summary>
public sealed class AvaloniaFilePicker : IFilePicker
{
    public async Task<string?> PickFileAsync(string title, string? startPath = null, IReadOnlyList<string>? extensions = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var provider = window.StorageProvider;
        if (provider is not { CanOpen: true })
        {
            return null;
        }

        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(startPath))
        {
            var startFolder = Path.GetDirectoryName(startPath);
            if (!string.IsNullOrWhiteSpace(startFolder))
            {
                start = await provider.TryGetFolderFromPathAsync(startFolder).ConfigureAwait(true);
            }
        }

        var fileTypes = extensions is { Count: > 0 }
            ? new List<FilePickerFileType>
            {
                new("Supported files") { Patterns = [.. extensions.Select(static ext => $"*{ext}")] },
            }
            : null;

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter = fileTypes,
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
