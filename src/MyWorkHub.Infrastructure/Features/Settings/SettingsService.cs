using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.Workspace;

namespace MyWorkHub.Infrastructure.Features.Settings;

/// <summary>
/// <see cref="ISettingsService"/> over the live <see cref="IConfiguration"/> (reads) and the user's
/// <c>appsettings.json</c> via <see cref="JsonSettingsFile"/> (writes). After every save the configuration is
/// reloaded explicitly, so the change is visible to the next read at once — the file watcher would get there
/// too, but only after its polling interval. Section and key names come from the options types that read them,
/// so the writer and the readers cannot drift apart.
/// </summary>
internal sealed class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly JsonSettingsFile _file;

    public SettingsService(IConfiguration configuration, JsonSettingsFile file)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(file);
        _configuration = configuration;
        _file = file;
    }

    public AppearanceSettings GetAppearance()
    {
        var ui = _configuration.GetSection(UiOptions.SECTION);
        return AppearanceSettings.Normalize(ui[nameof(UiOptions.Theme)], ui[nameof(UiOptions.Palette)]);
    }

    public WorkspaceSettings GetWorkspace()
    {
        var workspace = _configuration.GetSection(WorkspaceOptions.SECTION);
        return new WorkspaceSettings(
            Raw(workspace, nameof(WorkspaceOptions.ClaudeExecutablePath)),
            Raw(workspace, nameof(WorkspaceOptions.WorkFolderPath)),
            Raw(workspace, nameof(WorkspaceOptions.ReviewModelId)),
            Raw(workspace, nameof(WorkspaceOptions.ReviewAgentPath)));
    }

    public AzureDevOpsSettings GetAzureDevOps()
    {
        var azureDevOps = _configuration.GetSection(AzureDevOpsOptions.SECTION);
        List<string> projects = [.. azureDevOps.GetSection(nameof(AzureDevOpsOptions.Projects)).GetChildren()
            .Select(c => c.Value?.Trim())
            .OfType<string>()
            .Where(p => p.Length > 0)];
        return new AzureDevOpsSettings(Raw(azureDevOps, nameof(AzureDevOpsOptions.OrganizationUrl)), projects);
    }

    public Task SaveAppearanceAsync(AppearanceSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SaveAsync(root =>
        {
            var ui = JsonSettingsFile.Section(root, UiOptions.SECTION);
            JsonSettingsFile.Set(ui, nameof(UiOptions.Theme), settings.Theme);
            JsonSettingsFile.Set(ui, nameof(UiOptions.Palette), settings.Palette);
        }, ct);
    }

    public Task SaveWorkspaceAsync(WorkspaceSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SaveAsync(root =>
        {
            var workspace = JsonSettingsFile.Section(root, WorkspaceOptions.SECTION);
            JsonSettingsFile.Set(workspace, nameof(WorkspaceOptions.ClaudeExecutablePath), settings.ClaudeExecutablePath.Trim());
            JsonSettingsFile.Set(workspace, nameof(WorkspaceOptions.WorkFolderPath), settings.WorkFolderPath.Trim());
            JsonSettingsFile.Set(workspace, nameof(WorkspaceOptions.ReviewModelId), settings.ReviewModelId.Trim());
            JsonSettingsFile.Set(workspace, nameof(WorkspaceOptions.ReviewAgentPath), settings.ReviewAgentPath.Trim());
        }, ct);
    }

    public Task SaveAzureDevOpsAsync(AzureDevOpsSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SaveAsync(root =>
        {
            var azureDevOps = JsonSettingsFile.Section(root, AzureDevOpsOptions.SECTION);
            JsonSettingsFile.Set(azureDevOps, nameof(AzureDevOpsOptions.OrganizationUrl), settings.OrganizationUrl.Trim());
            JsonSettingsFile.Set(azureDevOps, nameof(AzureDevOpsOptions.Projects),
                new JsonArray([.. settings.Projects.Select(p => (JsonNode?)JsonValue.Create(p))]));
        }, ct);
    }

    private async Task SaveAsync(Action<JsonObject> update, CancellationToken ct)
    {
        await _file.UpdateAsync(update, ct).ConfigureAwait(false);

        // The array under AzureDevOps:Projects may have shrunk: a reload (not a merge) drops the stale entries.
        if (_configuration is IConfigurationRoot root)
        {
            root.Reload();
        }
    }

    private static string Raw(IConfigurationSection section, string key) => section[key]?.Trim() ?? "";
}
