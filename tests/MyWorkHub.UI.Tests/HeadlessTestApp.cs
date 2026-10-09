using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using MyWorkHub.UI.Tests;

// [AvaloniaFact] tests run on this headless application (no window server, no real App).
[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace MyWorkHub.UI.Tests;

/// <summary>Bare application for headless view tests: the Fluent theme, so real control templates take input.</summary>
public sealed class HeadlessTestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
