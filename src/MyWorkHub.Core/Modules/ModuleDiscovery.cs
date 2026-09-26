using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MyWorkHub.Core.Modules;

/// <summary>
/// Finds and instantiates feature modules (<see cref="IInfrastructureModule"/>,
/// <see cref="IPresentationModule"/>, <see cref="IViewModule"/>) by reflecting over assemblies.
/// Called once per layer at composition time.
/// </summary>
public static class ModuleDiscovery
{
    /// <summary>
    /// Returns one instance of every public-parameterless-constructible, non-abstract class in
    /// <paramref name="assemblies"/> that implements <typeparamref name="T"/>, ordered by full type
    /// name so registration order is deterministic. Duplicate assemblies are scanned once.
    /// </summary>
    [RequiresUnreferencedCode("Scans assemblies for module types via reflection; module types must not be trimmed.")]
    public static IReadOnlyList<T> Find<T>(IEnumerable<Assembly> assemblies)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .Where(IsModuleType<T>)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (T)Activator.CreateInstance(t)!)
            .ToList();
    }

    [RequiresUnreferencedCode("Reflects over constructors of arbitrary types.")]
    private static bool IsModuleType<T>(Type type)
        => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
           && typeof(T).IsAssignableFrom(type)
           && type.GetConstructor(Type.EmptyTypes) is not null;

    [RequiresUnreferencedCode("Enumerates every type in the assembly.")]
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // A type whose dependency is missing must not hide the modules that did load.
            return ex.Types.OfType<Type>();
        }
    }
}
