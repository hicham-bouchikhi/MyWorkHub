using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Composition;

/// <summary>
/// Startup check that the discovered modules fit together: every page view model — each registered
/// <see cref="PageViewModel"/> service plus every sidebar entry's target — must resolve from the
/// container and have a constructible view in the <see cref="ViewRegistry"/>. Any gap throws at
/// launch, naming the offending type, instead of surfacing later as a blank page or a click-time crash.
/// </summary>
public static class ShellCompositionValidator
{
    /// <summary>Validates the composed container; throws <see cref="InvalidOperationException"/> listing every problem.</summary>
    /// <param name="services">The service collection the provider was built from (used to enumerate page registrations).</param>
    /// <param name="provider">The built provider.</param>
    public static void Validate(IServiceCollection services, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);

        var registry = provider.GetRequiredService<ViewRegistry>();
        var menu = provider.GetRequiredService<IReadOnlyList<NavigationItem>>();

        var pageTypes = services
            .Select(d => d.ServiceType)
            .Where(t => typeof(PageViewModel).IsAssignableFrom(t))
            .Concat(menu.Select(i => i.ViewModelType))
            .Distinct();

        var problems = new List<string>();
        foreach (var pageType in pageTypes)
        {
            ValidatePage(pageType, provider, registry, problems);
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Module composition is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(p => " - " + p)));
        }
    }

    private static void ValidatePage(Type pageType, IServiceProvider provider, ViewRegistry registry, List<string> problems)
    {
        try
        {
            if (provider.GetRequiredService(pageType) is not ViewModelBase)
            {
                problems.Add($"'{pageType.FullName}' does not derive from {nameof(ViewModelBase)}.");
            }
        }
        catch (Exception ex)
        {
            problems.Add($"'{pageType.FullName}' cannot be resolved from the container: {ex.Message}");
        }

        if (!registry.TryGetViewType(pageType, out var viewType))
        {
            problems.Add($"'{pageType.FullName}' has no view registered by any IViewModule.");
            return;
        }

        if (ViewRegistry.DescribeUnconstructibleView(viewType) is { } reason)
        {
            problems.Add($"The view for '{pageType.FullName}' cannot be created: {reason}");
        }
    }
}
