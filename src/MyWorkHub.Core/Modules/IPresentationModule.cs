using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Core.Modules;

/// <summary>
/// One feature's presentation registrations (its page view model(s) and any presentation-only
/// helpers), plus the optional sidebar entry that navigates to it. Implemented once per feature inside
/// <c>MyWorkHub.Presentation</c> and discovered automatically by <see cref="ModuleDiscovery"/>.
/// Implementations must be non-abstract classes with a public parameterless constructor.
/// </summary>
public interface IPresentationModule
{
    /// <summary>The sidebar entry for this feature, or <c>null</c> when it has no top-level page.</summary>
    NavigationItem? MenuEntry { get; }

    /// <summary>Registers the feature's view model(s) with the container.</summary>
    void RegisterServices(IServiceCollection services);
}
