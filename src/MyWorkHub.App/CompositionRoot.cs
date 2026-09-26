using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MyWorkHub.Automation.Scheduling;
using MyWorkHub.Core;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Data;
using MyWorkHub.Infrastructure.DependencyInjection;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MyWorkHub.App;

/// <summary>
/// The dependency-injection composition root: wires configuration, logging, infrastructure, and the
/// UI graph (each layer discovering its own feature modules) into a single <see cref="ServiceProvider"/>,
/// then validates that the discovered modules fit together before the app starts.
/// </summary>
internal static class CompositionRoot
{
    [RequiresUnreferencedCode("Feature modules are discovered via reflection.")]
    public static ServiceProvider Build()
    {
        SeedUserConfig();
        ConfigureSerilog();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<UiOptions>(configuration.GetSection(UiOptions.SECTION));

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        // Infrastructure (EF Core + Data Protection credential store, then feature modules) must be
        // registered before anything that depends on credentials (ARCHITECTURE.md §12).
        services.AddInfrastructure(configuration);

        // UI shell: navigation, toast notifications, feature pages + views and the main window.
        services.AddUi();

        // Quartz scheduler + the discovered automation jobs and their configured schedules. Jobs resolve
        // their Core dependencies from this container; the scheduler is started by App once the UI is up.
        services.AddAutomations(configuration);

        var provider = services.BuildServiceProvider();
        try
        {
            // Migrate BEFORE validating: validation constructs every page view model, and some read the
            // database while constructing (e.g. Automations reads saved-login state from the credential
            // store) — on a first run the tables would not exist yet.
            InitializeDatabase(provider);

            // Fail loud at launch: every page view model must resolve and have a view.
            ShellCompositionValidator.Validate(services, provider);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup composition failed");
            provider.Dispose();
            throw;
        }

        return provider;
    }

    /// <summary>Applies any pending EF Core migrations.</summary>
    private static void InitializeDatabase(IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Database.Migrate();
    }

    /// <summary>
    /// Seeds the user folder with the shipped defaults on first run: the configuration and the built-in
    /// pull request review-agent template. Existing (possibly user-edited) copies are never overwritten.
    /// </summary>
    private static void SeedUserConfig()
    {
        SeedFile("appsettings.json", AppPaths.UserAppSettingsPath);
        SeedFile("review-agent.md", AppPaths.ReviewAgentPath);
    }

    private static void SeedFile(string shippedFileName, string userPath)
    {
        if (File.Exists(userPath))
        {
            return;
        }

        var shipped = Path.Combine(AppContext.BaseDirectory, shippedFileName);
        if (File.Exists(shipped))
        {
            File.Copy(shipped, userPath);
        }
    }

    /// <summary>
    /// Loads <c>~/.MyWorkHub/appsettings.json</c> with reload on change, so services that re-read their section
    /// per use (Azure DevOps, Workspace) see edits — from the Settings page, which also reloads explicitly after
    /// saving, or by hand — without a restart. The file is <em>polled</em> (every few seconds, that one file
    /// only) rather than watched with a <see cref="FileSystemWatcher"/>: a watcher on the data folder is
    /// recursive, and on Linux that means one inotify watch per directory of every PR-review clone under
    /// <c>repos/</c>, which can exhaust the per-user watch limit.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "The file provider backs the process-wide configuration and must live until the process exits; " +
                        "the configuration does not own it and the container cannot (it is needed before the container exists).")]
    private static IConfiguration BuildConfiguration()
    {
        var fileProvider = new PhysicalFileProvider(AppPaths.RootDir)
        {
            UsePollingFileWatcher = true,
            UseActivePolling = true,
        };

        return new ConfigurationBuilder()
            .AddJsonFile(fileProvider, "appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    private static void ConfigureSerilog()
        => Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(AppPaths.LogsDir, "MyWorkHub-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31)
            .CreateLogger();
}
