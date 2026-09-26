using Microsoft.Extensions.Configuration;
using MyWorkHub.Core;

namespace MyWorkHub.Infrastructure.Features.Workspace;

/// <summary>
/// Local tooling used by the AI features (configuration section <c>Workspace</c>). Read key by key rather
/// than through the reflection-based configuration binder. Blank values fall back to sensible defaults.
/// </summary>
/// <param name="ClaudeExecutablePath">The <c>claude</c> CLI to run; defaults to <c>claude</c> resolved on <c>PATH</c>.</param>
/// <param name="WorkFolderPath">Where pull request repositories are cloned; defaults to
/// <see cref="AppPaths.ReviewRepositoriesDir"/>.</param>
/// <param name="ReviewModelId">Model for pull request reviews; <c>null</c> keeps the CLI's default.</param>
/// <param name="ReviewAgentPath">User-chosen global review-agent template; <c>null</c> uses the built-in one.</param>
internal sealed record WorkspaceOptions(
    string ClaudeExecutablePath,
    string WorkFolderPath,
    string? ReviewModelId,
    string? ReviewAgentPath)
{
    public const string SECTION = "Workspace";

    private const string DEFAULT_CLAUDE_EXECUTABLE = "claude";

    public static WorkspaceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);
        return new WorkspaceOptions(
            Read(section, nameof(ClaudeExecutablePath)) ?? DEFAULT_CLAUDE_EXECUTABLE,
            Read(section, nameof(WorkFolderPath)) ?? AppPaths.ReviewRepositoriesDir,
            Read(section, nameof(ReviewModelId)),
            Read(section, nameof(ReviewAgentPath)));
    }

    private static string? Read(IConfigurationSection section, string key)
        => section[key]?.Trim() is { Length: > 0 } value ? value : null;
}
