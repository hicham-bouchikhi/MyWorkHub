using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → Workspace: the Claude CLI, where pull request reviews clone repositories, the review model and
/// the global review-agent template. Saved to <c>Workspace:*</c>; the next review / AI run uses the new values
/// without a restart (the consuming services re-read the section per use). Blank fields mean the built-in
/// default, shown as the field's placeholder.
/// </summary>
public sealed partial class WorkspaceSettingsViewModel : ViewModelBase
{
    private static readonly string[] _agentFileExtensions = [".md"];

    private readonly ISettingsService? _settings;
    private readonly IFolderPicker? _folderPicker;
    private readonly IFilePicker? _filePicker;

    public WorkspaceSettingsViewModel(
        ISettingsService? settings = null, IFolderPicker? folderPicker = null, IFilePicker? filePicker = null)
    {
        _settings = settings;
        _folderPicker = folderPicker;
        _filePicker = filePicker;
    }

    /// <summary>False when settings cannot be persisted; the panel then shows a notice.</summary>
    public bool IsAvailable => _settings is not null;

    public bool CanBrowseFolders => _folderPicker is not null;

    public bool CanBrowseFiles => _filePicker is not null;

    /// <summary>Where reviews clone when <see cref="WorkFolderPath"/> is blank.</summary>
    public static string DefaultWorkFolderPath => AppPaths.ReviewRepositoriesDir;

    /// <summary>The built-in review agent used when <see cref="ReviewAgentPath"/> is blank.</summary>
    public static string DefaultReviewAgentPath => AppPaths.ReviewAgentPath;

    [ObservableProperty]
    private string _claudeExecutablePath = "";

    /// <summary>Where pull request reviews clone repositories (<c>{folder}/{project}/{repository}</c>).</summary>
    [ObservableProperty]
    private string _workFolderPath = "";

    [ObservableProperty]
    private string _reviewModelId = "";

    [ObservableProperty]
    private string _reviewAgentPath = "";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public void Load()
    {
        if (_settings is null)
        {
            return;
        }

        var workspace = _settings.GetWorkspace();
        ClaudeExecutablePath = workspace.ClaudeExecutablePath;
        WorkFolderPath = workspace.WorkFolderPath;
        ReviewModelId = workspace.ReviewModelId;
        ReviewAgentPath = workspace.ReviewAgentPath;
        StatusMessage = null;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanBrowseFiles))]
    private async Task BrowseClaudeExecutableAsync()
    {
        if (await _filePicker!.PickFileAsync("Choose the Claude CLI executable", Blank(ClaudeExecutablePath)) is { } path)
        {
            ClaudeExecutablePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseFolders))]
    private async Task BrowseWorkFolderAsync()
    {
        var start = Blank(WorkFolderPath) ?? DefaultWorkFolderPath;
        if (await _folderPicker!.PickFolderAsync("Choose where pull request reviews clone repositories", start) is { } path)
        {
            WorkFolderPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseFiles))]
    private async Task BrowseReviewAgentAsync()
    {
        var start = Blank(ReviewAgentPath) ?? DefaultReviewAgentPath;
        if (await _filePicker!.PickFileAsync("Choose the review agent", start, _agentFileExtensions) is { } path)
        {
            ReviewAgentPath = path;
        }
    }

    [RelayCommand]
    private void ResetWorkFolder() => WorkFolderPath = "";

    [RelayCommand]
    private void ResetReviewAgent() => ReviewAgentPath = "";

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private async Task SaveAsync(CancellationToken ct)
    {
        var workspace = new WorkspaceSettings(
            ClaudeExecutablePath.Trim(), WorkFolderPath.Trim(), ReviewModelId.Trim(), ReviewAgentPath.Trim());
        if (Validate(workspace) is { } problem)
        {
            ErrorMessage = problem;
            StatusMessage = null;
            return;
        }

        try
        {
            await _settings!.SaveWorkspaceAsync(workspace, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not save the workspace settings: {ex.Message}";
            StatusMessage = null;
            return;
        }

        ClaudeExecutablePath = workspace.ClaudeExecutablePath;
        WorkFolderPath = workspace.WorkFolderPath;
        ReviewModelId = workspace.ReviewModelId;
        ReviewAgentPath = workspace.ReviewAgentPath;
        ErrorMessage = null;
        StatusMessage = $"Saved. The next pull request review clones into {Blank(workspace.WorkFolderPath) ?? DefaultWorkFolderPath}.";
    }

    /// <summary>Why <paramref name="workspace"/> cannot be saved, or null when it can. Values are already trimmed.</summary>
    internal static string? Validate(WorkspaceSettings workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        // A bare command name ("claude") is resolved on PATH; anything with a directory part must be absolute.
        var claude = workspace.ClaudeExecutablePath;
        if (claude.Length > 0 && !string.IsNullOrEmpty(Path.GetDirectoryName(claude)) && !Path.IsPathFullyQualified(claude))
        {
            return "The Claude CLI must be a command on PATH (e.g. claude) or a full path to the executable.";
        }

        var workFolder = workspace.WorkFolderPath;
        if (workFolder.Length > 0 && !Path.IsPathFullyQualified(workFolder))
        {
            return "The work folder must be a full path (or blank for the default).";
        }

        if (workFolder.Length > 0 && File.Exists(workFolder))
        {
            return $"The work folder '{workFolder}' is a file, not a folder.";
        }

        var agent = workspace.ReviewAgentPath;
        if (agent.Length > 0 && !(Path.IsPathFullyQualified(agent) && File.Exists(agent)))
        {
            return $"The review agent file '{agent}' does not exist (use a full path, or Reset for the built-in agent).";
        }

        return null;
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
