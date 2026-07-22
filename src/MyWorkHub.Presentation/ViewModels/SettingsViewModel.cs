using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Settings page (T064/T065). Tabbed shell — Integrations / Automations / Preferences —
/// with Azure DevOps and Microsoft 365 connection sections wired up. The org URL and
/// project list persist to appsettings.json; the PAT goes to the DPAPI credential store,
/// both via <see cref="IAzureDevOpsSettingsService"/>. Microsoft 365 sign-in is triggered
/// manually via <see cref="IGraphConnectionService"/>.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly IAzureDevOpsSettingsService? _settings;
    private readonly IGraphConnectionService? _graphConnection;
    private readonly IWorkspaceSettingsService? _workspaceSettings;
    private readonly IFolderPicker? _folderPicker;
    private readonly IEmailService? _emailService;
    private readonly IEmailSettingsService? _emailSettings;
    private readonly IThemeService? _themeService;

    // Palette key used when the requested display name is unknown.
    private const string DEFAULT_PALETTE = "GitHub";

    // ── Preferences: workspace (Claude Code review) ─────────────────────────
    [ObservableProperty]
    private string _workFolderPath = "";

    [ObservableProperty]
    private string _claudeExecutablePath = "";

    [ObservableProperty]
    private string _reviewModelId = "claude-sonnet-5";

    // ── Preferences: theme ──────────────────────────────────────────────────
    public IReadOnlyList<string> ThemeOptions { get; } = ["System", "Light", "Dark"];

    [ObservableProperty]
    private string _selectedTheme = "System";

    // ── Preferences: palette ─────────────────────────────────────────────────
    // Display name (shown in the picker) ⇄ persisted palette key (applied by the theme service / config).
    private static readonly Dictionary<string, string> _displayToKey = new()
    {
        ["GitHub"] = "GitHub",
        ["VS Code"] = "VSCode",
        ["One Dark Pro"] = "OneDark",
        ["Tokyo Night"] = "TokyoNight",
    };

    public IReadOnlyList<string> PaletteOptions { get; } = ["GitHub", "VS Code", "One Dark Pro", "Tokyo Night"];

    [ObservableProperty]
    private string _selectedPalette = "GitHub";

    // ── Integrations: Azure DevOps ──────────────────────────────────────────
    [ObservableProperty]
    private string _organizationUrl = "";

    [ObservableProperty]
    private string _personalAccessTokenInput = "";

    [ObservableProperty]
    private bool _hasExistingPat;

    [ObservableProperty]
    private string _newProject = "";

    [ObservableProperty]
    private string? _statusMessage;

    // ── Integrations: Microsoft 365 ─────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMicrosoftConnected))]
    [NotifyCanExecuteChangedFor(nameof(ConnectMicrosoftCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectMicrosoftCommand))]
    private string? _microsoftAccount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectMicrosoftCommand))]
    private bool _isMicrosoftConnecting;

    [ObservableProperty]
    private string? _microsoftStatusMessage;

    /// <summary>True when a Microsoft 365 account is cached and available.</summary>
    public bool IsMicrosoftConnected => MicrosoftAccount is not null;

    public SettingsViewModel(
        IAzureDevOpsSettingsService? settings = null,
        IGraphConnectionService? graphConnection = null,
        IOptions<UiOptions>? uiOptions = null,
        IWorkspaceSettingsService? workspaceSettings = null,
        IFolderPicker? folderPicker = null,
        IEmailService? emailService = null,
        IEmailSettingsService? emailSettings = null,
        IThemeService? themeService = null)
        : base("Settings")
    {
        _settings = settings;
        _graphConnection = graphConnection;
        _workspaceSettings = workspaceSettings;
        _folderPicker = folderPicker;
        _emailService = emailService;
        _emailSettings = emailSettings;
        _themeService = themeService;
        _selectedTheme = uiOptions?.Value.Theme ?? "System";
        _selectedPalette = DisplayNameForKey(uiOptions?.Value.Palette);

        if (_settings is not null)
        {
            var current = _settings.Get();
            OrganizationUrl = current.OrganizationUrl;
            HasExistingPat = current.HasPersonalAccessToken;
            foreach (var project in current.Projects)
            {
                Projects.Add(project);
            }
        }

        if (_workspaceSettings is not null)
        {
            var workspace = _workspaceSettings.Get();
            WorkFolderPath = workspace.WorkFolderPath;
            ClaudeExecutablePath = workspace.ClaudeExecutablePath;
            ReviewModelId = string.IsNullOrWhiteSpace(workspace.ReviewModelId)
                ? "claude-sonnet-5"
                : workspace.ReviewModelId;
        }

        if (_graphConnection is not null)
        {
            _ = LoadMicrosoftConnectionStateAsync();
        }

        if (_emailService is not null)
        {
            _ = LoadEmailFoldersAsync();
        }
    }

    private async Task LoadMicrosoftConnectionStateAsync()
    {
        MicrosoftAccount = await _graphConnection!.GetConnectedAccountAsync();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        _themeService?.ApplyTheme(value); // no-op when no theme host is wired (e.g. unit tests)
        PersistUiValue("Theme", value);
    }

    partial void OnSelectedPaletteChanged(string value)
    {
        var key = KeyForDisplayName(value);
        _themeService?.ApplyPalette(key); // no-op when no theme host is wired (e.g. unit tests)
        PersistUiValue("Palette", key);
    }

    private static string DisplayNameForKey(string? key)
    {
        foreach (var pair in _displayToKey)
        {
            if (string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return "GitHub";
    }

    private static string KeyForDisplayName(string displayName) =>
        _displayToKey.TryGetValue(displayName, out var key) ? key : DEFAULT_PALETTE;

    private static void PersistUiValue(string key, string value)
    {
        try
        {
            var path = AppPaths.UserAppSettingsPath;
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var node = JsonNode.Parse(json) ?? new JsonObject();

            node["UI"] ??= new JsonObject();
            node["UI"]![key] = value;

            File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Persistence failure is non-fatal; the change is already live in memory.
        }
    }

    /// <summary>Configured AzDO team project names (editable: add/remove).</summary>
    public ObservableCollection<string> Projects { get; } = [];

    [RelayCommand]
    private void AddProject()
    {
        var name = NewProject?.Trim();
        if (!string.IsNullOrEmpty(name) && !Projects.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Projects.Add(name);
        }

        NewProject = "";
    }

    [RelayCommand]
    private void RemoveProject(string? project)
    {
        if (project is not null)
        {
            Projects.Remove(project);
        }
    }

    [RelayCommand]
    private async Task BrowseWorkFolderAsync()
    {
        if (_folderPicker is null)
        {
            return;
        }

        var start = string.IsNullOrWhiteSpace(WorkFolderPath) ? null : WorkFolderPath;
        var picked = await _folderPicker.PickFolderAsync("Select the review work folder", start);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            WorkFolderPath = picked;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (_settings is null && _workspaceSettings is null)
        {
            StatusMessage = "Settings storage is unavailable.";
            return;
        }

        if (_settings is not null)
        {
            // A blank PAT field means "keep the existing token"; only send a value when typed.
            var pat = string.IsNullOrWhiteSpace(PersonalAccessTokenInput) ? null : PersonalAccessTokenInput;

            _settings.Save(OrganizationUrl, Projects, pat);

            if (pat is not null)
            {
                HasExistingPat = true;
            }

            PersonalAccessTokenInput = "";
        }

        _workspaceSettings?.Save(WorkFolderPath, ClaudeExecutablePath, ReviewModelId);

        StatusMessage = $"Saved at {DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}.";
    }

    [RelayCommand(CanExecute = nameof(CanConnectMicrosoft))]
    private async Task ConnectMicrosoftAsync(CancellationToken ct)
    {
        if (_graphConnection is null)
        {
            return;
        }

        IsMicrosoftConnecting = true;
        MicrosoftStatusMessage = null;
        try
        {
            var result = await _graphConnection.ConnectAsync(ct);
            if (result.Success)
            {
                MicrosoftAccount = result.AccountName;
            }
            else
            {
                MicrosoftStatusMessage = result.ErrorMessage ?? "Connection failed.";
            }
        }
        finally
        {
            IsMicrosoftConnecting = false;
        }
    }

    private bool CanConnectMicrosoft() => !IsMicrosoftConnecting && !IsMicrosoftConnected;

    [RelayCommand(CanExecute = nameof(IsMicrosoftConnected))]
    private async Task DisconnectMicrosoftAsync()
    {
        if (_graphConnection is null)
        {
            return;
        }

        await _graphConnection.DisconnectAsync();
        MicrosoftAccount = null;
        MicrosoftStatusMessage = null;
    }

    // ── Integrations: Email folders ─────────────────────────────────────────

    /// <summary>The user's mail folders, each toggleable for inclusion in the digest.</summary>
    public ObservableCollection<MailFolderRow> EmailFolders { get; } = [];

    [ObservableProperty]
    private string? _emailStatusMessage;

    [RelayCommand]
    private async Task LoadEmailFoldersAsync()
    {
        if (_emailService is null)
        {
            return;
        }

        EmailStatusMessage = null;
        try
        {
            var folders = await _emailService.GetMailFoldersAsync().ConfigureAwait(true);
            EmailFolders.Clear();
            foreach (var folder in folders)
            {
                EmailFolders.Add(new MailFolderRow(folder));
            }
        }
        catch (Exception)
        {
            EmailFolders.Clear();
            EmailStatusMessage = "Couldn't load your mail folders. Make sure Microsoft 365 is connected.";
        }
    }

    [RelayCommand]
    private void SaveEmailFolders()
    {
        if (_emailSettings is null)
        {
            EmailStatusMessage = "Settings storage is unavailable.";
            return;
        }

        var watched = new List<string>();
        CollectWatched(EmailFolders, watched);
        _emailSettings.Save(watched);
        EmailStatusMessage = $"Watching {watched.Count} folder(s) — saved at {DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)}.";
    }

    private static void CollectWatched(IEnumerable<MailFolderRow> rows, List<string> ids)
    {
        foreach (var row in rows)
        {
            // A node counts as watched unless it is fully unchecked: `true` (all) and
            // the indeterminate `null` (partial) both include the folder's own messages.
            if (row.IsWatched != false)
            {
                ids.Add(row.Id);
            }

            CollectWatched(row.Children, ids);
        }
    }
}

/// <summary>
/// A mail folder projected for the Settings picker. <see cref="IsWatched"/> is two-way bound
/// to a tri-state checkbox: <c>true</c> = the folder and all descendants are watched,
/// <c>false</c> = none is, <c>null</c> = a mix (indeterminate). Ticking a parent cascades to
/// every descendant; ticking a leaf bubbles the aggregate state back up to its ancestors.
/// </summary>
public sealed partial class MailFolderRow : ObservableObject
{
    // Re-entrancy guards for the two propagation directions, so cascading down and
    // recomputing up don't fight each other into an infinite loop.
    private bool _suppressChildCascade;
    private bool _suppressParentRecompute;

    public MailFolderRow(MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        Id = folder.Id;
        DisplayName = folder.DisplayName;
        UnreadItemCount = folder.UnreadItemCount;
        Children = folder.Children.Select(child => new MailFolderRow(child)).ToList();

        foreach (var child in Children)
        {
            child.Parent = this;
        }

        // A leaf keeps its stored flag; a parent derives its state from its children
        // (which are already built, so their states are final). Set the backing field
        // directly to avoid triggering cascade during construction.
        _isWatched = Children.Count == 0 ? folder.IsWatched : AggregateOfChildren();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public int UnreadItemCount { get; }

    /// <summary>Nested sub-folders shown as child nodes in the tree.</summary>
    public IReadOnlyList<MailFolderRow> Children { get; }

    /// <summary>The row this node hangs off, or <c>null</c> for a top-level folder.</summary>
    public MailFolderRow? Parent { get; private set; }

    [ObservableProperty]
    private bool? _isWatched;

    partial void OnIsWatchedChanged(bool? value)
    {
        // Cascade a definite tick/untick down to every descendant. The indeterminate
        // state is only ever set by an upward recompute, never pushed down.
        if (value.HasValue && !_suppressChildCascade)
        {
            foreach (var child in Children)
            {
                child.SetFromParent(value.Value);
            }
        }

        if (!_suppressParentRecompute)
        {
            Parent?.RecomputeFromChildren();
        }
    }

    private void SetFromParent(bool value)
    {
        _suppressParentRecompute = true; // don't bubble back up while the parent drives us
        try
        {
            IsWatched = value; // recurses down into our own children via OnIsWatchedChanged
        }
        finally
        {
            _suppressParentRecompute = false;
        }
    }

    private void RecomputeFromChildren()
    {
        _suppressChildCascade = true; // the value is derived from children — don't push it back down
        try
        {
            IsWatched = AggregateOfChildren(); // bubbles further up to our own parent
        }
        finally
        {
            _suppressChildCascade = false;
        }
    }

    private bool? AggregateOfChildren()
    {
        if (Children.All(child => child.IsWatched == true))
        {
            return true;
        }

        if (Children.All(child => child.IsWatched == false))
        {
            return false;
        }

        return null;
    }
}
