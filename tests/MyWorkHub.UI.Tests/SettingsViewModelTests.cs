using MyWorkHub.Core.Configuration;
using MyWorkHub.UI.ViewModels;
using Microsoft.Extensions.Options;

namespace MyWorkHub.UI.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Palette_options_lists_all_four_palettes()
    {
        var vm = new SettingsViewModel();

        Assert.Equal(["GitHub", "VS Code", "One Dark Pro", "Tokyo Night"], vm.PaletteOptions);
    }

    [Fact]
    public async Task Browse_work_folder_sets_the_path_when_a_folder_is_picked()
    {
        var picker = new FakeFolderPicker(@"C:\repos\cegid-reviews");
        var vm = new SettingsViewModel(folderPicker: picker);

        await vm.BrowseWorkFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\repos\cegid-reviews", vm.WorkFolderPath);
    }

    [Fact]
    public async Task Browse_work_folder_keeps_the_path_when_the_picker_is_cancelled()
    {
        var picker = new FakeFolderPicker(result: null);
        var vm = new SettingsViewModel(folderPicker: picker) { WorkFolderPath = @"C:\existing" };

        await vm.BrowseWorkFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\existing", vm.WorkFolderPath);
    }

    [Fact]
    public async Task Browse_review_agent_sets_the_path_when_a_file_is_picked()
    {
        var picker = new FakeFilePicker(@"C:\agents\security-review.md");
        var vm = new SettingsViewModel(filePicker: picker);

        await vm.BrowseReviewAgentCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\agents\security-review.md", vm.ReviewAgentPath);
        Assert.Equal([".md"], picker.LastExtensions);
    }

    [Fact]
    public async Task Browse_review_agent_keeps_the_path_when_the_picker_is_cancelled()
    {
        var picker = new FakeFilePicker(result: null);
        var vm = new SettingsViewModel(filePicker: picker) { ReviewAgentPath = @"C:\agents\existing.md" };

        await vm.BrowseReviewAgentCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\agents\existing.md", vm.ReviewAgentPath);
    }

    [Fact]
    public void ResetReviewAgent_clears_the_path()
    {
        var vm = new SettingsViewModel { ReviewAgentPath = @"C:\agents\existing.md" };

        vm.ResetReviewAgentCommand.Execute(null);

        Assert.Equal("", vm.ReviewAgentPath);
    }

    [Fact]
    public void Constructor_selects_palette_display_name_from_options_key()
    {
        var options = Options.Create(new UiOptions { Palette = "OneDark" });

        var vm = new SettingsViewModel(uiOptions: options);

        Assert.Equal("One Dark Pro", vm.SelectedPalette);
    }

    [Fact]
    public void Constructor_defaults_to_github_when_palette_key_is_unknown()
    {
        var options = Options.Create(new UiOptions { Palette = "not-a-palette" });

        var vm = new SettingsViewModel(uiOptions: options);

        Assert.Equal("GitHub", vm.SelectedPalette);
    }

    [Fact]
    public void Constructor_loads_current_settings()
    {
        var settings = new FakeAzureDevOpsSettingsService("https://dev.azure.com/cegid/", ["Alpha", "Beta"], hasPat: true);

        var vm = new SettingsViewModel(settings);

        Assert.Equal("https://dev.azure.com/cegid/", vm.OrganizationUrl);
        Assert.Equal(["Alpha", "Beta"], vm.Projects);
        Assert.True(vm.HasExistingPat);
    }

    [Fact]
    public void AddProject_adds_trimmed_unique_names_and_clears_input()
    {
        var vm = new SettingsViewModel(new FakeAzureDevOpsSettingsService());

        vm.NewProject = "  MyProject  ";
        vm.AddProjectCommand.Execute(null);
        vm.NewProject = "myproject"; // case-insensitive duplicate
        vm.AddProjectCommand.Execute(null);

        Assert.Equal(["MyProject"], vm.Projects);
        Assert.Equal("", vm.NewProject);
    }

    [Fact]
    public void RemoveProject_removes_the_named_project()
    {
        var vm = new SettingsViewModel(new FakeAzureDevOpsSettingsService(projects: ["Alpha", "Beta"]));

        vm.RemoveProjectCommand.Execute("Alpha");

        Assert.Equal(["Beta"], vm.Projects);
    }

    [Fact]
    public void Save_with_a_typed_pat_persists_it_and_clears_the_field()
    {
        var settings = new FakeAzureDevOpsSettingsService();
        var vm = new SettingsViewModel(settings)
        {
            OrganizationUrl = "https://dev.azure.com/cegid/",
            PersonalAccessTokenInput = "secret-pat",
        };
        vm.Projects.Add("Alpha");

        vm.SaveCommand.Execute(null);

        Assert.True(settings.SaveCalled);
        Assert.Equal("secret-pat", settings.SavedPat);
        Assert.True(vm.HasExistingPat);
        Assert.Equal("", vm.PersonalAccessTokenInput);
        Assert.NotNull(vm.StatusMessage);
        Assert.Equal(["Alpha"], settings.Get().Projects);
    }

    [Fact]
    public void Save_with_a_blank_pat_leaves_the_existing_token_untouched()
    {
        var settings = new FakeAzureDevOpsSettingsService(hasPat: true);
        var vm = new SettingsViewModel(settings); // PAT field left blank

        vm.SaveCommand.Execute(null);

        Assert.True(settings.SaveCalled);
        Assert.Null(settings.SavedPat); // null => "keep existing"
    }

    // ── Microsoft 365 connection ────────────────────────────────────────────

    [Fact]
    public void Constructor_without_graph_service_initialises_to_not_connected()
    {
        var vm = new SettingsViewModel();

        Assert.Null(vm.MicrosoftAccount);
        Assert.False(vm.IsMicrosoftConnected);
    }

    [Fact]
    public async Task ConnectMicrosoft_on_success_sets_account_and_clears_status()
    {
        var graph = new FakeGraphConnectionService(connectSucceeds: true);
        var vm = new SettingsViewModel(graphConnection: graph);

        await vm.ConnectMicrosoftCommand.ExecuteAsync(null);

        Assert.True(graph.ConnectCalled);
        Assert.Equal("user@contoso.com", vm.MicrosoftAccount);
        Assert.True(vm.IsMicrosoftConnected);
        Assert.Null(vm.MicrosoftStatusMessage);
    }

    [Fact]
    public async Task ConnectMicrosoft_on_failure_sets_status_message_and_leaves_account_null()
    {
        var graph = new FakeGraphConnectionService(connectSucceeds: false, connectError: "AADSTS50011");
        var vm = new SettingsViewModel(graphConnection: graph);

        await vm.ConnectMicrosoftCommand.ExecuteAsync(null);

        Assert.Null(vm.MicrosoftAccount);
        Assert.False(vm.IsMicrosoftConnected);
        Assert.Equal("AADSTS50011", vm.MicrosoftStatusMessage);
    }

    [Fact]
    public async Task DisconnectMicrosoft_clears_account_and_calls_service()
    {
        var graph = new FakeGraphConnectionService();
        var vm = new SettingsViewModel(graphConnection: graph);
        vm.MicrosoftAccount = "user@contoso.com"; // simulate pre-loaded connected state

        await vm.DisconnectMicrosoftCommand.ExecuteAsync(null);

        Assert.True(graph.DisconnectCalled);
        Assert.Null(vm.MicrosoftAccount);
        Assert.False(vm.IsMicrosoftConnected);
    }
}
