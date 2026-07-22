using System.Globalization;
using MyWorkHub.Core;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Data;
using MyWorkHub.Infrastructure.DependencyInjection;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;

namespace MyWorkHub.App;

/// <summary>
/// The dependency-injection composition root: wires configuration, logging,
/// infrastructure, and the UI graph into a single <see cref="ServiceProvider"/>.
/// </summary>
internal static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        SeedUserConfig();
        SeedReviewAgent();
        ConfigureSerilog();

        var configuration = BuildConfiguration();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SECTION));
        services.Configure<AzureDevOpsOptions>(configuration.GetSection(AzureDevOpsOptions.SECTION));
        services.Configure<AutomationOptions>(configuration.GetSection(AutomationOptions.SECTION));
        services.Configure<ExternalSitesOptions>(configuration.GetSection(ExternalSitesOptions.SECTION));
        services.Configure<UiOptions>(configuration.GetSection(UiOptions.SECTION));
        services.Configure<WorkspaceOptions>(configuration.GetSection(WorkspaceOptions.SECTION));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SECTION));

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        // Infrastructure (EF Core + DPAPI credential store) must be registered
        // before anything that depends on credentials (ARCHITECTURE.md §12).
        services.AddInfrastructure();

        // UI shell: navigation, toast notifications, pages and the main window.
        services.AddUi();

        // Quartz scheduler: registers ISchedulerFactory (singleton) with a Microsoft DI
        // job factory so jobs resolve from the container. Jobs are scheduled in later phases.
        services.AddQuartz();

        return services.BuildServiceProvider();
    }

    /// <summary>Applies any pending EF Core migrations on startup.</summary>
    public static void InitializeDatabase(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var db = services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Database.Migrate();
    }

    /// <summary>Seeds the user config folder with the shipped defaults on first run.</summary>
    private static void SeedUserConfig()
    {
        if (File.Exists(AppPaths.UserAppSettingsPath))
        {
            return;
        }

        var shipped = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(shipped))
        {
            File.Copy(shipped, AppPaths.UserAppSettingsPath);
        }
    }

    /// <summary>Seeds the Claude Code review-agent template on first run.</summary>
    private static void SeedReviewAgent()
    {
        if (File.Exists(AppPaths.ReviewAgentPath))
        {
            return;
        }

        var shipped = Path.Combine(AppContext.BaseDirectory, "review-agent.md");
        if (File.Exists(shipped))
        {
            File.Copy(shipped, AppPaths.ReviewAgentPath);
        }
    }

    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .SetBasePath(AppPaths.RootDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

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
