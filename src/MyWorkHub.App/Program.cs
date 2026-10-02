using Avalonia;
using MyWorkHub.Core;
using Serilog;

namespace MyWorkHub.App;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't
    // initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppPaths.EnsureCreated();
        ConfigureWebKit();

        using var singleInstance = SingleInstanceGuard.Acquire();
        if (singleInstance.IsAlreadyRunning)
            return;

        // Also migrates the database, before startup validation constructs the pages.
        var services = CompositionRoot.Build();
        try
        {
            BuildAvaloniaApp(services, singleInstance).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            services.Dispose();
            Log.CloseAndFlush();
        }
    }

    // The email reader's WebKitGTK paints an empty pane with accelerated compositing on NVIDIA drivers under
    // Wayland/XWayland. It must be in the *native* environment before WebKit starts (WebKit reads it with getenv and
    // its web process inherits it): on Unix, Environment.SetEnvironmentVariable only updates .NET's own copy, so
    // libc setenv is called instead. A value the user exported wins (overwrite = 0).
    private static void ConfigureWebKit()
    {
        if (OperatingSystem.IsLinux())
        {
            _ = NativeMethods.setenv("WEBKIT_DISABLE_COMPOSITING_MODE", "1", overwrite: 0);
        }
    }

    // Avalonia configuration for the running application (DI-aware).
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services, SingleInstanceGuard singleInstance)
        => AppBuilder.Configure(() => new App(services, singleInstance))
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    // Parameterless entry point used by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
