using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.Infrastructure.Features.Calendar;

/// <summary>Registers the Graph-backed calendar service. The feature has no configuration section.</summary>
public sealed class CalendarInfrastructureModule : IInfrastructureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Shared clock seam; TryAdd so any feature module may declare the same need.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICalendarService, GraphCalendarService>();
    }
}
