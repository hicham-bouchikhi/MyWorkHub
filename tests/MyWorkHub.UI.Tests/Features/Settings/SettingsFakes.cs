using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Settings;

/// <summary>In-memory settings store that records every save, in order, and can be made to fail.</summary>
internal sealed class FakeSettingsService : ISettingsService
{
    public AppearanceSettings Appearance { get; set; } = AppearanceSettings.Default;

    public WorkspaceSettings Workspace { get; set; } = new("", "", "claude-sonnet-5", "");

    public AzureDevOpsSettings AzureDevOps { get; set; } = new("https://dev.azure.com/cegid/", []);

    public EmailSettings Email { get; set; } = new(["inbox"], 25);

    /// <summary>Every save, as the record passed in, in call order (shared with other fakes via <see cref="Log"/>).</summary>
    public List<object> Saves { get; } = [];

    /// <summary>Optional shared event log, to assert ordering against other fakes.</summary>
    public List<string>? Log { get; set; }

    public Exception? Failure { get; set; }

    public AppearanceSettings GetAppearance() => Appearance;

    public WorkspaceSettings GetWorkspace() => Workspace;

    public AzureDevOpsSettings GetAzureDevOps() => AzureDevOps;

    public EmailSettings GetEmail() => Email;

    public Task SaveAppearanceAsync(AppearanceSettings settings, CancellationToken ct = default)
        => Record(settings, () => Appearance = settings);

    public Task SaveWorkspaceAsync(WorkspaceSettings settings, CancellationToken ct = default)
        => Record(settings, () => Workspace = settings);

    public Task SaveAzureDevOpsAsync(AzureDevOpsSettings settings, CancellationToken ct = default)
        => Record(settings, () => AzureDevOps = settings);

    public Task SaveEmailAsync(EmailSettings settings, CancellationToken ct = default)
        => Record(settings, () => Email = settings);

    private Task Record(object settings, Action apply)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Saves.Add(settings);
        Log?.Add("save " + settings.GetType().Name);
        apply();
        return Task.CompletedTask;
    }
}

internal sealed class RecordingThemeService : IThemeService
{
    public List<string> Themes { get; } = [];

    public List<string> Palettes { get; } = [];

    public void ApplyTheme(string theme) => Themes.Add(theme);

    public void ApplyPalette(string paletteKey) => Palettes.Add(paletteKey);
}

internal sealed class FakeFolderPicker(string? answer) : IFolderPicker
{
    public List<(string Title, string? StartPath)> Requests { get; } = [];

    public Task<string?> PickFolderAsync(string title, string? startPath = null)
    {
        Requests.Add((title, startPath));
        return Task.FromResult(answer);
    }
}

internal sealed class FakeFilePicker(string? answer) : IFilePicker
{
    public List<(string Title, string? StartPath, IReadOnlyList<string>? Extensions)> Requests { get; } = [];

    public Task<string?> PickFileAsync(string title, string? startPath = null, IReadOnlyList<string>? extensions = null)
    {
        Requests.Add((title, startPath, extensions));
        return Task.FromResult(answer);
    }
}
