namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Cross-platform single-file chooser. Implemented in the UI layer over Avalonia's
/// <c>IStorageProvider</c>, so it works on Windows, macOS, and Linux. Returns the selected
/// local file path, or <c>null</c> when the user cancels or no picker is available.
/// </summary>
public interface IFilePicker
{
    Task<string?> PickFileAsync(string title, string? startPath = null, IReadOnlyList<string>? extensions = null);
}
