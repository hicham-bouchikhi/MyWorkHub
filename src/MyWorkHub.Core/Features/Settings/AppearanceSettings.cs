namespace MyWorkHub.Core.Features.Settings;

/// <summary>The persisted look of the app (configuration keys <c>UI:Theme</c> and <c>UI:Palette</c>).</summary>
/// <param name="Theme">One of <see cref="Themes"/>: <c>System</c> follows the OS, otherwise forced Light or Dark.</param>
/// <param name="Palette">One of <see cref="Palettes"/>: the colour palette applied on top of the theme variant.</param>
public sealed record AppearanceSettings(string Theme, string Palette)
{
    /// <summary>Theme variants, in the order the Settings page lists them.</summary>
    public static IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];

    /// <summary>
    /// Colour palette keys, in the order the Settings page lists them. Each key names a palette dictionary
    /// the UI host ships (<c>UI/Themes/{key}.axaml</c>).
    /// </summary>
    public static IReadOnlyList<string> Palettes { get; } = ["GitHub", "VSCode", "OneDark", "TokyoNight"];

    /// <summary>What a fresh install uses.</summary>
    public static AppearanceSettings Default { get; } = new(Themes[0], Palettes[0]);

    /// <summary>
    /// Maps possibly hand-edited values onto the canonical choices (case-insensitively); unknown or blank
    /// values fall back to <see cref="Default"/>.
    /// </summary>
    public static AppearanceSettings Normalize(string? theme, string? palette)
        => new(Canonical(Themes, theme) ?? Default.Theme, Canonical(Palettes, palette) ?? Default.Palette);

    private static string? Canonical(IReadOnlyList<string> choices, string? value)
        => choices.FirstOrDefault(c => string.Equals(c, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
