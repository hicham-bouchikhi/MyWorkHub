using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → Azure DevOps: organization URL, the projects whose pull requests are listed, and the personal
/// access token. The URL and projects are saved to <c>AzureDevOps:*</c> and used by the Pull requests and Work
/// items pages on their next load, without a restart. The token goes through the same
/// <see cref="IAzureDevOpsConnectionService"/> as those pages' inline prompt (one credential-store key), so
/// both places always agree; verifying it first saves the URL, since the token is checked against it.
/// </summary>
public sealed partial class AzureDevOpsSettingsViewModel : ViewModelBase
{
    private const string AZURE_DEVOPS_HOST = "dev.azure.com";

    private readonly ISettingsService? _settings;
    private readonly IAzureDevOpsConnectionService? _connection;

    public AzureDevOpsSettingsViewModel(ISettingsService? settings = null, IAzureDevOpsConnectionService? connection = null)
    {
        _settings = settings;
        _connection = connection;
    }

    /// <summary>False when settings cannot be persisted; the panel then shows a notice.</summary>
    public bool IsAvailable => _settings is not null;

    /// <summary>Whether the token part of the panel is shown.</summary>
    public bool CanManageToken => _connection is not null;

    [ObservableProperty]
    private string _organizationUrl = "";

    /// <summary>Project names, in the order they are queried.</summary>
    public ObservableCollection<string> Projects { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddProjectCommand))]
    private string _newProject = "";

    /// <summary>The token being typed; cleared once accepted so it does not linger in memory.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _personalAccessToken = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _hasStoredToken;

    [ObservableProperty]
    private string? _tokenStatus;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public void Load()
    {
        if (_settings is not null)
        {
            var azureDevOps = _settings.GetAzureDevOps();
            OrganizationUrl = azureDevOps.OrganizationUrl;
            ReplaceProjects(azureDevOps.Projects);
        }

        StatusMessage = null;
        ErrorMessage = null;
        RefreshTokenStatus();
    }

    [RelayCommand(CanExecute = nameof(CanAddProject))]
    private void AddProject()
    {
        AddProjectName(NewProject);
        NewProject = "";
    }

    private bool CanAddProject() => !string.IsNullOrWhiteSpace(NewProject);

    [RelayCommand]
    private void RemoveProject(string project) => Projects.Remove(project);

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private async Task SaveAsync(CancellationToken ct) => await TrySaveAsync(ct);

    /// <summary>Saves the URL and projects, then verifies and stores the token against that organization.</summary>
    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        if (!await TrySaveAsync(ct))
        {
            return;
        }

        IsBusy = true;
        AzureDevOpsConnectResult result;
        try
        {
            result = await _connection!.ConnectAsync(PersonalAccessToken, ct);
        }
        finally
        {
            IsBusy = false;
        }

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            RefreshTokenStatus();
            return;
        }

        PersonalAccessToken = "";
        HasStoredToken = true;
        TokenStatus = $"Connected as {result.DisplayName}. The token is stored encrypted on this machine.";
    }

    private bool CanConnect()
        => IsAvailable && _connection is not null && !IsBusy && !string.IsNullOrWhiteSpace(PersonalAccessToken);

    [RelayCommand(CanExecute = nameof(HasStoredToken))]
    private void Disconnect()
    {
        _connection?.Disconnect();
        RefreshTokenStatus();
    }

    /// <summary>
    /// Normalizes an organization URL (trimmed, trailing <c>/</c>) and rejects what cannot work: blank, not an
    /// absolute http(s) URL, or <c>https://dev.azure.com/</c> without the organization segment.
    /// </summary>
    internal static bool TryNormalizeOrganizationUrl(
        string raw, [NotNullWhen(true)] out string? normalized, [NotNullWhen(false)] out string? error)
    {
        normalized = null;
        var text = raw?.Trim() ?? "";
        if (text.Length == 0)
        {
            error = "Enter your organization URL, e.g. https://dev.azure.com/your-org/.";
            return false;
        }

        if (!text.EndsWith('/'))
        {
            text += "/";
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
            || url.Query.Length > 0 || url.Fragment.Length > 0)
        {
            error = $"'{raw?.Trim()}' is not a valid organization URL. Use the form https://dev.azure.com/your-org/.";
            return false;
        }

        if (string.Equals(url.Host, AZURE_DEVOPS_HOST, StringComparison.OrdinalIgnoreCase) && url.AbsolutePath == "/")
        {
            error = "Include your organization in the URL: https://dev.azure.com/your-org/.";
            return false;
        }

        normalized = url.AbsoluteUri;
        error = null;
        return true;
    }

    /// <summary>Trimmed, non-blank, de-duplicated (case-insensitively, first spelling wins) project names.</summary>
    internal static IReadOnlyList<string> CleanProjects(IEnumerable<string> projects)
        => [.. projects.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)];

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        if (_settings is null)
        {
            return false;
        }

        // A name typed but not yet added with "+" is clearly meant to be kept.
        if (!string.IsNullOrWhiteSpace(NewProject))
        {
            AddProject();
        }

        if (!TryNormalizeOrganizationUrl(OrganizationUrl, out var url, out var error))
        {
            ErrorMessage = error;
            StatusMessage = null;
            return false;
        }

        var projects = CleanProjects(Projects);
        try
        {
            await _settings.SaveAzureDevOpsAsync(new AzureDevOpsSettings(url, projects), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not save the Azure DevOps settings: {ex.Message}";
            StatusMessage = null;
            return false;
        }

        OrganizationUrl = url;
        ReplaceProjects(projects);
        ErrorMessage = null;
        StatusMessage = projects.Count == 0
            ? "Saved. Add at least one project to list your pull requests (work items do not need one)."
            : "Saved. Pull requests and Work items use it on their next load — press Refresh on a page that is already open.";
        return true;
    }

    private void AddProjectName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length > 0 && !Projects.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            Projects.Add(trimmed);
        }
    }

    private void ReplaceProjects(IEnumerable<string> projects)
    {
        Projects.Clear();
        foreach (var project in projects)
        {
            AddProjectName(project);
        }
    }

    private void RefreshTokenStatus()
    {
        HasStoredToken = _connection?.HasStoredToken ?? false;
        TokenStatus = HasStoredToken
            ? "A token is stored (encrypted on this machine). Enter a new one to replace it."
            : "No token stored. Create one with Code (Read) and Work Items (Read) scopes.";
    }
}
