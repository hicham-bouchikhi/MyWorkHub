namespace MyWorkHub.Core.Abstractions;

/// <summary>Current workspace settings, projected for the Settings form.</summary>
public sealed record WorkspaceSettings(
    string WorkFolderPath,
    string ClaudeExecutablePath,
    string ReviewModelId);

/// <summary>
/// Reads and persists the code-review workspace settings. Values go to
/// <c>appsettings.json</c>; saving also updates the live configuration so the review
/// service picks up the new values without an app restart. Mirrors
/// <see cref="IAzureDevOpsSettingsService"/>.
/// </summary>
public interface IWorkspaceSettingsService
{
    /// <summary>The current work folder, claude executable path, and review model id.</summary>
    WorkspaceSettings Get();

    /// <summary>Persists the workspace settings and refreshes the live configuration.</summary>
    void Save(string workFolderPath, string claudeExecutablePath, string reviewModelId);
}
