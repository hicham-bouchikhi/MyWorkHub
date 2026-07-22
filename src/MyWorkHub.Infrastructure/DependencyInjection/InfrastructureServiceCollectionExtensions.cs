using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Ai;
using MyWorkHub.Infrastructure.AzureDevOps;
using MyWorkHub.Infrastructure.CodeReview;
using MyWorkHub.Infrastructure.Data;
using MyWorkHub.Infrastructure.Graph;
using MyWorkHub.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace MyWorkHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers EF Core (SQLite), the Data Protection credential store, Graph and Azure DevOps services.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DbPath}"));

        // Cross-platform credential encryption. The key ring is persisted under the app's
        // runtime folder and (on Windows) itself DPAPI-protected; SetApplicationName pins
        // the key isolation so keys stay stable across launches and app versions.
        AppPaths.EnsureCreated();
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(AppPaths.DataProtectionKeysDir))
            .SetApplicationName("MyWorkHub");
        services.AddSingleton<ICredentialStore, DataProtectionCredentialStore>();

        // Personal todo list — local-only persistence over the same SQLite database.
        services.AddSingleton<ITodoRepository, TodoRepository>();

        // Automation run history — local-only. Registered before Graph so the dashboard's
        // optional automation widget lights up as soon as it is available.
        services.AddSingleton<IAutomationLogger, AutomationLogger>();

        AddGraph(services);
        AddAzureDevOps(services);
        AddCodeReview(services);

        // AI email digest via the Claude CLI (same binary/auth as code review). Resolves even
        // when the CLI is absent — the failure surfaces only when a summary is requested.
        services.AddSingleton<IEmailSummaryService, ClaudeCliEmailSummaryService>();

        return services;
    }

    private static void AddCodeReview(IServiceCollection services)
    {
        // Claude Code PR review + workspace settings. Singleton so the settings service
        // shares the one WorkspaceOptions instance with the review service for live updates.
        services.AddSingleton<IPrReviewService, ClaudeCodePrReviewService>();
        services.AddSingleton<IWorkspaceSettingsService, WorkspaceSettingsService>();
    }

    private static void AddGraph(IServiceCollection services)
    {
        // MSAL public client + DPAPI-backed token cache. Resolution is lazy, so a
        // missing AzureAD:ClientId only surfaces when Graph is first used, not at startup.
        services.AddSingleton<IPublicClientApplication>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAdOptions>>().Value;
            var app = MsalPublicClientFactory.Create(options);
            MsalPublicClientFactory.RegisterTokenCacheAsync(app).GetAwaiter().GetResult();
            return app;
        });

        services.AddSingleton(sp =>
            GraphServiceClientFactory.Create(sp.GetRequiredService<IPublicClientApplication>()));

        services.AddSingleton<IGraphConnectionService, GraphConnectionService>();
        services.AddSingleton<IEmailService, GraphEmailService>();

        // Singleton so it shares the one EmailOptions instance with GraphEmailService — a saved
        // folder change takes effect immediately, no restart.
        services.AddSingleton<IEmailSettingsService, EmailSettingsService>();
        services.AddSingleton<ICalendarService, GraphCalendarService>();
        services.AddSingleton<ITeamsService, GraphTeamsService>();
    }

    private static void AddAzureDevOps(IServiceCollection services)
    {
        // Typed HttpClient — no SDK. PAT is read per-request from the credential store.
        services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>();

        // Reads/writes the AzDO settings (PAT + org URL + projects). Singleton so it shares
        // the one AzureDevOpsOptions instance with the service for live updates (T065).
        services.AddSingleton<IAzureDevOpsSettingsService, AzureDevOpsSettingsService>();

        // Persistent read-state for work item @mention comments.
        services.AddSingleton<ISeenMentionRepository, SeenMentionRepository>();
    }
}
