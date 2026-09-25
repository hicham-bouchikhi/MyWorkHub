using MyWorkHub.Core.Features.Settings;

namespace MyWorkHub.Core.Tests.Features.Settings;

public sealed class AppearanceSettingsTests
{
    [Theory]
    [InlineData("Dark", "OneDark", "Dark", "OneDark")]
    [InlineData(" light ", "vscode", "Light", "VSCode")]
    [InlineData(null, null, "System", "GitHub")]
    [InlineData("Purple", "Solarized", "System", "GitHub")]
    public void Should_map_values_onto_the_known_choices_when_normalizing(
        string? theme, string? palette, string expectedTheme, string expectedPalette)
    {
        Assert.Equal(new AppearanceSettings(expectedTheme, expectedPalette), AppearanceSettings.Normalize(theme, palette));
    }

    [Fact]
    public void Should_default_to_the_first_theme_and_palette()
    {
        Assert.Equal(new AppearanceSettings("System", "GitHub"), AppearanceSettings.Default);
    }
}
