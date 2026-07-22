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

        using var singleInstance = SingleInstanceGuard.Acquire();
        if (singleInstance.IsAlreadyRunning)
            return;

        var services = CompositionRoot.Build();
        try
        {
            CompositionRoot.InitializeDatabase(services);
            BuildAvaloniaApp(services, singleInstance).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            services.Dispose();
            Log.CloseAndFlush();
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
