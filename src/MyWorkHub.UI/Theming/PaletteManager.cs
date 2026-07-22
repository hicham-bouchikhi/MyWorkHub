using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace MyWorkHub.UI.Theming;

/// <summary>
/// Swaps the active colour palette by merging the matching palette <see cref="ResourceDictionary"/>
/// into <see cref="Application"/> resources. Every palette defines the same token keys for both the
/// Light and Dark <c>ThemeVariant</c>, so consumers that use <c>DynamicResource</c> re-tint live and
/// the active theme variant selects the right sub-dictionary.
/// </summary>
public static class PaletteManager
{
    /// <summary>Palette key used when the requested one is missing or unknown.</summary>
    internal const string DEFAULT_PALETTE = "GitHub";

    private static readonly Uri _baseUri = new("avares://MyWorkHub.UI/");

    private static readonly Dictionary<string, Uri> _paletteUris =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["GitHub"] = new("avares://MyWorkHub.UI/Themes/GitHub.axaml"),
            ["VSCode"] = new("avares://MyWorkHub.UI/Themes/VSCode.axaml"),
            ["OneDark"] = new("avares://MyWorkHub.UI/Themes/OneDark.axaml"),
            ["TokyoNight"] = new("avares://MyWorkHub.UI/Themes/TokyoNight.axaml"),
        };

    private static ResourceInclude? _current;

    /// <summary>
    /// Merges the palette identified by <paramref name="paletteKey"/> (falling back to
    /// <see cref="DEFAULT_PALETTE"/> when unknown), replacing any previously applied palette.
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
            uri = _paletteUris[DEFAULT_PALETTE];
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
