using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.Features.Settings;

namespace MyWorkHub.UI.Tests.Features.Settings;

public sealed class AppearanceSettingsViewModelTests
{
    private readonly FakeSettingsService _settings = new() { Appearance = new AppearanceSettings("Light", "VSCode") };
    private readonly RecordingThemeService _theme = new();

    private AppearanceSettingsViewModel Loaded()
    {
        var vm = new AppearanceSettingsViewModel(_settings, _theme);
        vm.Load();
        return vm;
    }

    [Fact]
    public void Should_show_the_persisted_choice_without_applying_or_saving_it_when_loading()
    {
        var vm = Loaded();

        Assert.Equal("Light", vm.SelectedTheme);
        Assert.Equal("VSCode", vm.SelectedPalette);
        Assert.Empty(_theme.Themes);
        Assert.Empty(_theme.Palettes);
        Assert.Empty(_settings.Saves);
    }

    [Fact]
    public async Task Should_apply_the_theme_live_and_persist_it_when_a_theme_is_chosen()
    {
        var vm = Loaded();

        vm.SelectedTheme = "Dark";
        await vm.LastSave;

        Assert.Equal(["Dark"], _theme.Themes);
        Assert.Equal(new AppearanceSettings("Dark", "VSCode"), Assert.Single(_settings.Saves));
        Assert.Equal(new AppearanceSettings("Dark", "VSCode"), _settings.GetAppearance());
    }

    [Fact]
    public async Task Should_apply_the_palette_live_and_persist_it_when_a_palette_is_chosen()
    {
        var vm = Loaded();

        vm.SelectedPalette = "TokyoNight";
        await vm.LastSave;

        Assert.Equal(["TokyoNight"], _theme.Palettes);
        Assert.Equal(new AppearanceSettings("Light", "TokyoNight"), Assert.Single(_settings.Saves));
    }

    [Fact]
    public async Task Should_keep_the_applied_choice_and_explain_when_it_cannot_be_saved()
    {
        var vm = Loaded();
        _settings.Failure = new IOException("disk full");

        vm.SelectedTheme = "Dark";
        await vm.LastSave;

        Assert.Equal(["Dark"], _theme.Themes);
        Assert.Contains("disk full", vm.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_offer_every_theme_and_palette()
    {
        Assert.Equal(["System", "Light", "Dark"], AppearanceSettingsViewModel.Themes);
        Assert.Equal(["GitHub", "VSCode", "OneDark", "TokyoNight"], AppearanceSettingsViewModel.Palettes);
    }

    [Fact]
    public async Task Should_still_apply_live_but_save_nothing_when_no_settings_service_is_registered()
    {
        var vm = new AppearanceSettingsViewModel(settings: null, _theme);

        vm.SelectedTheme = "Dark";
        await vm.LastSave;

        Assert.False(vm.IsAvailable);
        Assert.Equal(["Dark"], _theme.Themes);
    }
}
