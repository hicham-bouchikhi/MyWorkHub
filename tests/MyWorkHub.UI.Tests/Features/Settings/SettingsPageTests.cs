using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Settings;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Tests.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class SettingsPageTests
{
    [Fact]
    public void Should_contribute_a_sidebar_entry_just_above_developer_and_a_view_in_the_default_composition()
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        var entries = provider.GetRequiredService<IReadOnlyList<NavigationItem>>().OrderBy(i => i.Order).ToList();
        Assert.Equal(typeof(SettingsViewModel), entries[^2].ViewModelType);
        Assert.Equal("Developer", entries[^1].Label);
        Assert.Equal(typeof(UI.Features.Settings.SettingsView),
            provider.GetRequiredService<ViewRegistry>().GetViewType(typeof(SettingsViewModel)));
        ShellCompositionValidator.Validate(services, provider);
    }

    [Fact]
    public void Should_wire_the_host_services_into_every_section_when_composed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService>(new FakeSettingsService());
        services.AddSingleton<IAzureDevOpsConnectionService>(new FakeAzureDevOpsConnection());
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        var page = provider.GetRequiredService<SettingsViewModel>();

        Assert.True(page.Appearance.IsAvailable);
        Assert.True(page.Workspace.IsAvailable);
        Assert.True(page.Workspace.CanBrowseFolders);
        Assert.True(page.Workspace.CanBrowseFiles);
        Assert.True(page.AzureDevOps.CanManageToken);
        Assert.True(page.About.CanOpenRepository);
    }

    [Fact]
    public void Should_reload_every_section_from_the_current_settings_when_shown()
    {
        var settings = new FakeSettingsService();
        var page = new SettingsViewModel(settings);
        settings.Appearance = new AppearanceSettings("Dark", "OneDark");
        settings.Workspace = new WorkspaceSettings("claude", "/clones", "m", "");
        settings.AzureDevOps = new AzureDevOpsSettings("https://dev.azure.com/acme/", ["Alpha"]);
        settings.Email = new EmailSettings(["inbox"], 40, ShowFavorites: true);

        page.LoadCommand.Execute(null);

        Assert.Equal(40, page.Email.MaxPerFolder);
        Assert.True(page.Email.ShowFavorites);

        Assert.Equal("OneDark", page.Appearance.SelectedPalette);
        Assert.Equal("/clones", page.Workspace.WorkFolderPath);
        Assert.Equal(["Alpha"], page.AzureDevOps.Projects);
        Assert.Empty(settings.Saves);
    }

    [Fact]
    public void Should_degrade_to_notices_when_no_service_is_registered()
    {
        var page = new SettingsViewModel();
        page.LoadCommand.Execute(null);

        Assert.Equal("Settings", page.Title);
        Assert.False(page.Appearance.IsAvailable);
        Assert.False(page.Workspace.IsAvailable);
        Assert.False(page.AzureDevOps.IsAvailable);
        Assert.False(page.About.CanOpenRepository);
    }
}
