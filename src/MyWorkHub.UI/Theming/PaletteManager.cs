using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using MyWorkHub.Core.Features.Settings;

namespace MyWorkHub.UI.Theming;

/// <summary>
/// Swaps the active colour palette by merging the matching palette <see cref="ResourceDictionary"/>
/// into <see cref="Application"/> resources. Every palette defines the same token keys for both the
/// Light and Dark <c>ThemeVariant</c>, so consumers that use <c>DynamicResource</c> re-tint live and
/// the active theme variant selects the right sub-dictionary.
/// </summary>
public static class PaletteManager
{
    private static readonly Uri _baseUri = new("avares://MyWorkHub.UI/");

    // One dictionary per palette key offered by the Settings page: UI/Themes/{key}.axaml.
    private static readonly Dictionary<string, Uri> _paletteUris = AppearanceSettings.Palettes.ToDictionary(
        key => key,
        key => new Uri($"avares://MyWorkHub.UI/Themes/{key}.axaml"),
        StringComparer.OrdinalIgnoreCase);

    private static ResourceInclude? _current;

    /// <summary>
    /// Merges the palette identified by <paramref name="paletteKey"/> (falling back to
    /// <see cref="AppearanceSettings.Default"/>'s palette when unknown), replacing any previously applied palette.
    /// No-op when there is no running <see cref="Application"/> (e.g. unit tests).
    /// </summary>
    public static void Apply(string? paletteKey)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (paletteKey is null || !_paletteUris.TryGetValue(paletteKey, out var uri))
        {
            uri = _paletteUris[AppearanceSettings.Default.Palette];
        }

        var include = new ResourceInclude(_baseUri) { Source = uri };

        // Add the new palette before removing the old one so DynamicResource lookups never see a gap.
        app.Resources.MergedDictionaries.Add(include);
        if (_current is not null)
        {
            app.Resources.MergedDictionaries.Remove(_current);
        }

        _current = include;
    }
}
