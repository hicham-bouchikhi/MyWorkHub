namespace MyWorkHub.Core.Configuration;

public sealed class UiOptions
{
    public const string SECTION = "UI";

    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>Persisted theme choice. Values: "System" | "Light" | "Dark".</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Persisted colour palette key. Values: "GitHub" | "VSCode" | "OneDark" | "TokyoNight".</summary>
    public string Palette { get; set; } = "GitHub";
}
