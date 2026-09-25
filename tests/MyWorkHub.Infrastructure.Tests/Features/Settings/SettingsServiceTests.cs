using System.Text.Json;
using System.Text.Json.Nodes;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Infrastructure.Features.Settings;

namespace MyWorkHub.Infrastructure.Tests.Features.Settings;

/// <summary>Round-trips through a real <c>appsettings.json</c> on disk.</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly SettingsFileFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    private static JsonObject Read(string path) => Parse(File.ReadAllText(path));

    private JsonObject OnDisk() => Read(_fixture.FilePath);

    [Fact]
    public async Task Should_update_only_the_workspace_keys_and_preserve_every_other_section_when_saving_the_workspace()
    {
        var before = Parse(SettingsFileFixture.SHIPPED_LIKE_JSON);

        await _fixture.Service.SaveWorkspaceAsync(
            new WorkspaceSettings("/opt/claude/bin/claude", " /src/reviews ", "claude-opus-5", ""),
            TestContext.Current.CancellationToken);

        var after = OnDisk();
        Assert.Equal(["AzureAD", "AzureDevOps", "Automation", "UI", "Workspace", "Email"], after.Select(p => p.Key));
        foreach (var untouched in new[] { "AzureAD", "AzureDevOps", "Automation", "UI", "Email" })
        {
            Assert.True(JsonNode.DeepEquals(before[untouched], after[untouched]), untouched + " changed");
        }

        Assert.Equal("/opt/claude/bin/claude", (string?)after["Workspace"]!["ClaudeExecutablePath"]);
        Assert.Equal("/src/reviews", (string?)after["Workspace"]!["WorkFolderPath"]);
        Assert.Equal("claude-opus-5", (string?)after["Workspace"]!["ReviewModelId"]);
        Assert.Equal("", (string?)after["Workspace"]!["ReviewAgentPath"]);
    }

    [Fact]
    public async Task Should_read_back_the_saved_workspace_immediately_when_saved()
    {
        await _fixture.Service.SaveWorkspaceAsync(
            new WorkspaceSettings("claude", "/src/reviews", "", "/home/me/agent.md"), TestContext.Current.CancellationToken);

        Assert.Equal(new WorkspaceSettings("claude", "/src/reviews", "", "/home/me/agent.md"), _fixture.Service.GetWorkspace());
    }

    [Fact]
    public async Task Should_replace_the_project_list_and_forget_removed_projects_when_saving_azure_devops()
    {
        var ct = TestContext.Current.CancellationToken;
        await _fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha", "Beta", "Gamma"]), ct);

        await _fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Beta"]), ct);

        var saved = _fixture.Service.GetAzureDevOps();
        Assert.Equal("https://dev.azure.com/acme/", saved.OrganizationUrl);
        Assert.Equal(["Beta"], saved.Projects);
        Assert.Equal(["Beta"], OnDisk()["AzureDevOps"]!["Projects"]!.AsArray().Select(p => (string?)p));
    }

    [Fact]
    public async Task Should_persist_the_appearance_and_keep_the_other_ui_keys_when_saving_the_appearance()
    {
        await _fixture.Service.SaveAppearanceAsync(new AppearanceSettings("Dark", "TokyoNight"), TestContext.Current.CancellationToken);

        Assert.Equal(new AppearanceSettings("Dark", "TokyoNight"), _fixture.Service.GetAppearance());
        var ui = OnDisk()["UI"]!;
        Assert.Equal(5, (int)ui["RefreshIntervalMinutes"]!);
        Assert.True((bool)ui["MinimizeToTrayOnClose"]!);
    }

    [Fact]
    public void Should_normalize_hand_edited_appearance_values_when_reading()
    {
        using var fixture = new SettingsFileFixture("""{ "UI": { "Theme": " dark ", "Palette": "NoSuchPalette" } }""");

        Assert.Equal(new AppearanceSettings("Dark", "GitHub"), fixture.Service.GetAppearance());
    }

    [Fact]
    public async Task Should_update_an_existing_section_whatever_its_key_casing_when_saving()
    {
        using var fixture = new SettingsFileFixture("""{ "workspace": { "reviewmodelid": "old", "Custom": 1 } }""");

        await fixture.Service.SaveWorkspaceAsync(new WorkspaceSettings("", "", "new", ""), TestContext.Current.CancellationToken);

        var root = Read(fixture.FilePath);
        var workspace = Assert.Single(root).Value!.AsObject();
        Assert.Equal("new", (string?)workspace["reviewmodelid"]);
        Assert.Equal(1, (int)workspace["Custom"]!);
        Assert.False(workspace.ContainsKey("ReviewModelId"));
    }

    [Fact]
    public async Task Should_create_the_file_when_it_does_not_exist_yet()
    {
        using var fixture = new SettingsFileFixture(initialJson: null);

        await fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha"]), TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha"], fixture.Service.GetAzureDevOps().Projects);
    }

    [Fact]
    public async Task Should_leave_a_malformed_file_untouched_when_saving()
    {
        const string broken = """{ "UI": { "Theme": "Dark" """;
        using var fixture = new SettingsFileFixture(SettingsFileFixture.SHIPPED_LIKE_JSON);
        await File.WriteAllTextAsync(fixture.FilePath, broken, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<JsonException>(() =>
            fixture.Service.SaveAppearanceAsync(new AppearanceSettings("Light", "GitHub"), TestContext.Current.CancellationToken));

        Assert.Equal(broken, await File.ReadAllTextAsync(fixture.FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_accept_comments_and_trailing_commas_like_the_configuration_loader_when_saving()
    {
        using var fixture = new SettingsFileFixture("""
            {
              // hand-written note
              "UI": { "Theme": "Light", },
            }
            """);

        await fixture.Service.SaveAppearanceAsync(new AppearanceSettings("Dark", "OneDark"), TestContext.Current.CancellationToken);

        Assert.Equal("Dark", (string?)Read(fixture.FilePath)["UI"]!["Theme"]);
    }

    [Fact]
    public async Task Should_keep_non_ascii_text_readable_and_leave_no_temp_file_behind_when_saving()
    {
        await _fixture.Service.SaveAppearanceAsync(new AppearanceSettings("Light", "VSCode"), TestContext.Current.CancellationToken);

        var text = await File.ReadAllTextAsync(_fixture.FilePath, TestContext.Current.CancellationToken);
        Assert.Contains("{{subscription_start}} au {{subscription_end}} (été)", text, StringComparison.Ordinal);
        Assert.Equal([_fixture.FilePath], Directory.GetFiles(_fixture.Directory.FullName));
    }

    [Fact]
    public async Task Should_keep_both_changes_when_two_sections_are_saved_at_the_same_time()
    {
        var ct = TestContext.Current.CancellationToken;

        await Task.WhenAll(
            Task.Run(() => _fixture.Service.SaveAppearanceAsync(new AppearanceSettings("Dark", "OneDark"), ct), ct),
            Task.Run(() => _fixture.Service.SaveWorkspaceAsync(new WorkspaceSettings("", "/src/reviews", "", ""), ct), ct),
            Task.Run(() => _fixture.Service.SaveAzureDevOpsAsync(new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha"]), ct), ct));

        Assert.Equal(new AppearanceSettings("Dark", "OneDark"), _fixture.Service.GetAppearance());
        Assert.Equal("/src/reviews", _fixture.Service.GetWorkspace().WorkFolderPath);
        Assert.Equal(["Alpha"], _fixture.Service.GetAzureDevOps().Projects);
    }

    [Fact]
    public void Should_register_the_settings_service_through_its_module()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        new SettingsInfrastructureModule().RegisterServices(services, _fixture.Configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(ISettingsService));
    }
}
