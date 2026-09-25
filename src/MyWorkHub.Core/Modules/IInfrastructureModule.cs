using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.Core.Modules;

/// <summary>
/// One feature's infrastructure registrations (e.g. <c>EmailInfrastructureModule</c>). Implemented once
/// per feature inside <c>MyWorkHub.Infrastructure</c> and discovered automatically by
/// <see cref="ModuleDiscovery"/> — adding a feature never means editing a shared registration file.
/// Implementations must be non-abstract classes with a public parameterless constructor.
/// </summary>
public interface IInfrastructureModule
{
    /// <summary>Registers the feature's service implementations and binds its configuration section(s).</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
