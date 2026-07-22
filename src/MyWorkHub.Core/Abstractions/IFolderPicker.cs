namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Cross-platform folder chooser. Implemented in the UI layer over Avalonia's
/// <c>IStorageProvider</c>, so it works on Windows, macOS, and Linux. Returns the selected
/// local folder path, or <c>null</c> when the user cancels or no picker is available.
/// </summary>
public interface IFolderPicker
{
    Task<string?> PickFolderAsync(string title, string? startPath = null);
}
