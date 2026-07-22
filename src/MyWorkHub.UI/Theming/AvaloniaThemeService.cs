using Avalonia;
using Avalonia.Styling;
using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.UI.Theming;

/// <summary>
/// Avalonia implementation of <see cref="IThemeService"/>: switches the application theme variant
/// and merges the requested colour palette. No-op when there is no running <see cref="Application"/>
/// (e.g. unit tests) — mirroring the guard the settings view model used before the seam was extracted.
/// </summary>
internal sealed class AvaloniaThemeService : IThemeService
{
    public void ApplyTheme(string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    public void ApplyPalette(string paletteKey) => PaletteManager.Apply(paletteKey);
}
