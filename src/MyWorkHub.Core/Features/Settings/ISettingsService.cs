namespace MyWorkHub.Core.Features.Settings;

/// <summary>
/// Reads and edits the user-editable parts of <c>~/.MyWorkHub/appsettings.json</c>. Reads reflect the current
/// file (the configuration reloads when it changes); a save rewrites only the keys it owns — every other
/// section and key is preserved — atomically, and takes effect without a restart for every service that
/// reads the edited section per use (Azure DevOps, Workspace; appearance is applied live by the caller).
/// Secrets never pass through here: the Azure DevOps token stays in the credential store.
/// </summary>
public interface ISettingsService
{
    /// <summary>The persisted theme and palette, normalized onto the known choices.</summary>
    AppearanceSettings GetAppearance();

    /// <summary>The stored <c>Workspace</c> values, blank where unset.</summary>
    WorkspaceSettings GetWorkspace();

    /// <summary>The stored <c>AzureDevOps</c> values, blank / empty where unset.</summary>
    AzureDevOpsSettings GetAzureDevOps();

    /// <summary>Writes <c>UI:Theme</c> and <c>UI:Palette</c>.</summary>
    Task SaveAppearanceAsync(AppearanceSettings settings, CancellationToken ct = default);

    /// <summary>Writes the four <c>Workspace:*</c> keys.</summary>
    Task SaveWorkspaceAsync(WorkspaceSettings settings, CancellationToken ct = default);

    /// <summary>Writes <c>AzureDevOps:OrganizationUrl</c> and replaces <c>AzureDevOps:Projects</c>.</summary>
    Task SaveAzureDevOpsAsync(AzureDevOpsSettings settings, CancellationToken ct = default);
}
