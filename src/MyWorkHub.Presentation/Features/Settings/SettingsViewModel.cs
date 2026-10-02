using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// The Settings page: one place for every user-editable setting, as tabs — Appearance, Workspace, Azure DevOps,
/// Email and About. Values are re-read from the configuration each time the page is shown (<see cref="LoadCommand"/>,
/// run by the view), so a hand edit of <c>appsettings.json</c> is reflected too. Every section saves on its own.
/// Nothing here needs a restart: appearance is applied live, and the Workspace / Azure DevOps services re-read
/// their section on every use (Email on the page's next refresh).
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    public SettingsViewModel(
        ISettingsService? settings = null,
        IThemeService? theme = null,
        IFolderPicker? folderPicker = null,
        IFilePicker? filePicker = null,
        IAzureDevOpsConnectionService? azureDevOpsConnection = null,
        IBrowserLauncher? browser = null,
        IEmailService? email = null)
        : base("Settings")
    {
        Appearance = new AppearanceSettingsViewModel(settings, theme);
        Workspace = new WorkspaceSettingsViewModel(settings, folderPicker, filePicker);
        AzureDevOps = new AzureDevOpsSettingsViewModel(settings, azureDevOpsConnection);
        Email = new EmailSettingsViewModel(settings, email);
        About = new AboutViewModel(browser);
    }

    public AppearanceSettingsViewModel Appearance { get; }

    public WorkspaceSettingsViewModel Workspace { get; }

    public AzureDevOpsSettingsViewModel AzureDevOps { get; }

    public EmailSettingsViewModel Email { get; }

    public AboutViewModel About { get; }

    /// <summary>Shows the current configuration in every section (discarding unsaved edits).</summary>
    [RelayCommand]
    private void Load()
    {
        Appearance.Load();
        Workspace.Load();
        AzureDevOps.Load();
        Email.Load();
        Email.LoadFoldersCommand.Execute(null);
    }
}
