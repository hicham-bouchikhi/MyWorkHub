namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Applies the user's chosen visual theme and colour palette to the running UI. Implemented per
/// host (Avalonia desktop today, a terminal front-end later); when no implementation is wired the
/// callers treat it as a no-op.
/// </summary>
public interface IThemeService
{
    /// <summary>Applies the theme variant: <c>"Light"</c>, <c>"Dark"</c>, or otherwise the system default.</summary>
    void ApplyTheme(string theme);

    /// <summary>Applies the colour palette identified by its key (e.g. <c>"GitHub"</c>, <c>"VSCode"</c>).</summary>
    void ApplyPalette(string paletteKey);
}
