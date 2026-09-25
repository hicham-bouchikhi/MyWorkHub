namespace MyWorkHub.Core.Features.Settings;

/// <summary>
/// The local tooling used by the AI features, exactly as stored under the <c>Workspace</c> configuration
/// section. A blank value means "use the built-in default" (resolved by the consuming services, not here).
/// </summary>
/// <param name="ClaudeExecutablePath">The <c>claude</c> CLI to run; blank = <c>claude</c> on <c>PATH</c>.</param>
/// <param name="WorkFolderPath">Where pull request reviews clone repositories; blank = <see cref="AppPaths.ReviewRepositoriesDir"/>.</param>
/// <param name="ReviewModelId">Model for pull request reviews; blank = the CLI's default model.</param>
/// <param name="ReviewAgentPath">Global review-agent Markdown template; blank = the built-in one.</param>
public sealed record WorkspaceSettings(
    string ClaudeExecutablePath,
    string WorkFolderPath,
    string ReviewModelId,
    string ReviewAgentPath);
