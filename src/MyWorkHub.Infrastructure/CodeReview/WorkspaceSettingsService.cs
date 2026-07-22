using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.CodeReview;

/// <summary>
/// <see cref="IWorkspaceSettingsService"/>: persists the code-review workspace settings to
/// the user's appsettings.json. The same <see cref="WorkspaceOptions"/> instance is shared
/// (via <see cref="IOptions{T}"/>) with the review service, so mutating it here makes the new
/// values take effect immediately — no app restart needed. Mirrors
/// <see cref="AzureDevOps.AzureDevOpsSettingsService"/>.
/// </summary>
public sealed class WorkspaceSettingsService : IWorkspaceSettingsService
{
    private readonly WorkspaceOptions _options;

    public WorkspaceSettingsService(IOptions<WorkspaceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public WorkspaceSettings Get()
        => new(_options.WorkFolderPath, _options.ClaudeExecutablePath, _options.ReviewModelId);

    public void Save(string workFolderPath, string claudeExecutablePath, string reviewModelId)
    {
        ArgumentNullException.ThrowIfNull(workFolderPath);
        ArgumentNullException.ThrowIfNull(claudeExecutablePath);
        ArgumentNullException.ThrowIfNull(reviewModelId);

        var cleanedFolder = workFolderPath.Trim();
        var cleanedExe = claudeExecutablePath.Trim();
        var cleanedModel = reviewModelId.Trim();

        // Update the live (shared) options so the review service sees the change at once.
        _options.WorkFolderPath = cleanedFolder;
        _options.ClaudeExecutablePath = cleanedExe;
        _options.ReviewModelId = cleanedModel;

        // Persist for the next launch.
        UserAppSettingsFile.SaveWorkspace(cleanedFolder, cleanedExe, cleanedModel);
    }
}
