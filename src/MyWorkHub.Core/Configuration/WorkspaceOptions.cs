namespace MyWorkHub.Core.Configuration;

/// <summary>
/// Settings for the local code-review workspace: where PR repos are cloned, which
/// <c>claude</c> executable to run (empty = resolve <c>claude</c> from PATH), and the
/// model id passed to the Claude Code CLI when reviewing a pull request.
/// </summary>
public sealed class WorkspaceOptions
{
    public const string SECTION = "Workspace";

    /// <summary>Folder under which each PR's repository is cloned/fetched for review.</summary>
    public string WorkFolderPath { get; set; } = "";

    /// <summary>Full path to the <c>claude</c> executable. Empty means resolve <c>claude</c> from PATH.</summary>
    public string ClaudeExecutablePath { get; set; } = "";

    /// <summary>Model id passed to <c>claude --model</c> for reviews.</summary>
    public string ReviewModelId { get; set; } = "claude-sonnet-5";
}
