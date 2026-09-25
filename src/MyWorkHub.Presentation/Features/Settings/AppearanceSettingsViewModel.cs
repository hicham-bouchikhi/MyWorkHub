using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → Appearance: theme variant and colour palette. A new choice is applied to the running UI at
/// once through <see cref="IThemeService"/> and persisted to <c>UI:Theme</c> / <c>UI:Palette</c> in the same
/// step (no Save button), so it also survives a restart.
/// </summary>
public sealed partial class AppearanceSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService? _settings;
    private readonly IThemeService? _theme;
    private bool _isLoading;

    public AppearanceSettingsViewModel(ISettingsService? settings = null, IThemeService? theme = null)
    {
        _settings = settings;
        _theme = theme;
    }

    /// <summary>False when settings cannot be persisted; the panel then shows a notice.</summary>
    public bool IsAvailable => _settings is not null;

    public static IReadOnlyList<string> Themes => AppearanceSettings.Themes;

    public static IReadOnlyList<string> Palettes => AppearanceSettings.Palettes;

    [ObservableProperty]
    private string _selectedTheme = AppearanceSettings.Default.Theme;

    [ObservableProperty]
    private string _selectedPalette = AppearanceSettings.Default.Palette;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>The most recent save (completed when idle); lets tests await the fire-on-change persistence.</summary>
    internal Task LastSave { get; private set; } = Task.CompletedTask;

    /// <summary>Shows the persisted choice without re-applying or re-saving it.</summary>
    public void Load()
    {
        if (_settings is null)
        {
            return;
        }

        var appearance = _settings.GetAppearance();
        _isLoading = true;
        try
        {
            SelectedTheme = appearance.Theme;
            SelectedPalette = appearance.Palette;
        }
        finally
        {
            _isLoading = false;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        _theme?.ApplyTheme(value);
        Persist();
    }

    partial void OnSelectedPaletteChanged(string value)
    {
        if (_isLoading || value is null)
        {
            return;
        }

        _theme?.ApplyPalette(value);
        Persist();
    }

    private void Persist()
    {
        if (_settings is not null)
        {
            LastSave = PersistAsync(_settings, new AppearanceSettings(SelectedTheme, SelectedPalette));
        }
    }

    private async Task PersistAsync(ISettingsService settings, AppearanceSettings appearance)
    {
        try
        {
            await settings.SaveAppearanceAsync(appearance);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Applied, but could not be saved for the next start: {ex.Message}";
        }
    }
}
