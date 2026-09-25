using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Core.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyWorkHub.Infrastructure.Features.Todo;

/// <summary>Registers the EF Core-backed todo repository. The feature has no configuration section.</summary>
public sealed class TodoInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Shared clock seam; TryAdd so any feature module may declare the same need.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITodoRepository, TodoRepository>();
    }
}
