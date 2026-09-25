using MyWorkHub.Core;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class WorkspaceSettingsViewModelTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("workspace-settings-test-");
    private readonly FakeSettingsService _settings = new();

    public void Dispose() => _directory.Delete(recursive: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string AgentFile()
    {
        var path = Path.Combine(_directory.FullName, "agent.md");
        File.WriteAllText(path, "agent");
        return path;
    }

    private WorkspaceSettingsViewModel Loaded(FakeFolderPicker? folders = null, FakeFilePicker? files = null)
    {
        var vm = new WorkspaceSettingsViewModel(_settings, folders, files);
        vm.Load();
        return vm;
    }

    [Fact]
    public async Task Should_save_the_folder_picked_as_the_clone_location_when_browsing_then_saving()
    {
        var clones = Path.Combine(_directory.FullName, "clones");
        var folders = new FakeFolderPicker(clones);
        var vm = Loaded(folders);

        await vm.BrowseWorkFolderCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppPaths.ReviewRepositoriesDir, Assert.Single(folders.Requests).StartPath);
        Assert.Equal(clones, _settings.Workspace.WorkFolderPath);
        Assert.Contains(clones, vm.StatusMessage, StringComparison.Ordinal);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Should_keep_the_typed_folder_when_the_folder_picker_is_cancelled()
    {
        var vm = Loaded(new FakeFolderPicker(answer: null));
        vm.WorkFolderPath = "/typed";

        await vm.BrowseWorkFolderCommand.ExecuteAsync(null);

        Assert.Equal("/typed", vm.WorkFolderPath);
    }

    [Fact]
    public async Task Should_pick_a_markdown_review_agent_and_save_it_trimmed()
    {
        var agent = AgentFile();
        var files = new FakeFilePicker(agent);
        var vm = Loaded(files: files);
        vm.ReviewModelId = "  claude-opus-5  ";

        await vm.BrowseReviewAgentCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Equal([".md"], Assert.Single(files.Requests).Extensions);
        Assert.Equal(new WorkspaceSettings("", "", "claude-opus-5", agent), Assert.Single(_settings.Saves));
        Assert.Equal("claude-opus-5", vm.ReviewModelId);
    }

    [Fact]
    public async Task Should_save_blank_values_meaning_the_defaults_when_reset()
    {
        _settings.Workspace = new WorkspaceSettings("claude", "/old/clones", "", AgentFile());
        var vm = Loaded();

        vm.ResetWorkFolderCommand.Execute(null);
        vm.ResetReviewAgentCommand.Execute(null);
        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Equal(new WorkspaceSettings("claude", "", "", ""), Assert.Single(_settings.Saves));
        Assert.Contains(AppPaths.ReviewRepositoriesDir, vm.StatusMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("")]
    public async Task Should_accept_a_bare_command_name_or_blank_for_the_claude_cli(string claude)
    {
        var vm = Loaded();
        vm.ClaudeExecutablePath = claude;

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(_settings.Saves);
    }

    [Theory]
    [InlineData("bin/claude", "", "", "Claude CLI")]
    [InlineData("", "relative/clones", "", "full path")]
    [InlineData("", "", "missing-agent.md", "does not exist")]
    public async Task Should_reject_the_save_and_explain_when_a_path_is_invalid(
        string claude, string workFolder, string agent, string expected)
    {
        var vm = Loaded();
        vm.ClaudeExecutablePath = claude;
        vm.WorkFolderPath = workFolder;
        vm.ReviewAgentPath = agent;

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Contains(expected, vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(_settings.Saves);
    }

    [Fact]
    public async Task Should_reject_a_work_folder_that_is_a_file()
    {
        var vm = Loaded();
        vm.WorkFolderPath = AgentFile();

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Contains("is a file", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(_settings.Saves);
    }

    [Fact]
    public async Task Should_explain_when_the_save_fails()
    {
        var vm = Loaded();
        _settings.Failure = new UnauthorizedAccessException("read-only");

        await vm.SaveCommand.ExecuteAsync(Ct);

        Assert.Contains("read-only", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(vm.StatusMessage);
    }

    [Fact]
    public void Should_disable_saving_and_browsing_when_their_services_are_missing()
    {
        var vm = new WorkspaceSettingsViewModel();

        Assert.False(vm.IsAvailable);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.False(vm.BrowseWorkFolderCommand.CanExecute(null));
        Assert.False(vm.BrowseReviewAgentCommand.CanExecute(null));
        Assert.False(vm.BrowseClaudeExecutableCommand.CanExecute(null));
    }
}
