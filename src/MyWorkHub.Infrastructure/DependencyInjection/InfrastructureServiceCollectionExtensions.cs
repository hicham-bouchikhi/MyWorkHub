using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Modules;
using MyWorkHub.Infrastructure.Data;
using MyWorkHub.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cross-cutting infrastructure (EF Core over SQLite, the Data Protection credential
    /// store), then every discovered <see cref="IInfrastructureModule"/>. Feature modules are registered
    /// last so they can consume the credential store.
    /// </summary>
    /// <param name="services">The container being composed.</param>
    /// <param name="configuration">App configuration, handed to each module to bind its own section(s).</param>
    /// <param name="moduleAssemblies">Assemblies to scan for modules; defaults to this (Infrastructure) assembly.</param>
    [RequiresUnreferencedCode("Discovers infrastructure modules via reflection.")]
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly>? moduleAssemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        var assemblies = moduleAssemblies ?? [typeof(InfrastructureServiceCollectionExtensions).Assembly];
        foreach (var module in ModuleDiscovery.Find<IInfrastructureModule>(assemblies))
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }
}
