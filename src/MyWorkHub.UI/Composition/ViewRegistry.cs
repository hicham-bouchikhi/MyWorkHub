using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using MyWorkHub.Core.Modules;

namespace MyWorkHub.UI.Composition;

/// <summary>
/// The merged view-model → view map, aggregated from every discovered <see cref="IViewModule"/>.
/// Replaces the old name-convention guessing in <see cref="ViewLocator"/> with explicit entries.
/// A view model mapped by two modules is a composition bug and throws on construction.
/// </summary>
public sealed class ViewRegistry
{
    private readonly Dictionary<Type, Type> _map = [];

    public ViewRegistry(IEnumerable<IViewModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            foreach (var (viewModelType, viewType) in module.ViewModelToView)
            {
                if (!_map.TryAdd(viewModelType, viewType))
                {
                    throw new InvalidOperationException(
                        $"View model '{viewModelType.FullName}' is mapped twice: to '{_map[viewModelType].FullName}' " +
                        $"and to '{viewType.FullName}' (by {module.GetType().FullName}).");
                }
            }
        }
    }

    /// <summary>Every registered mapping.</summary>
    public IReadOnlyDictionary<Type, Type> ViewModelToView => _map;

    public bool TryGetViewType(Type viewModelType, [NotNullWhen(true)] out Type? viewType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        return _map.TryGetValue(viewModelType, out viewType);
    }

    /// <summary>Returns the view type for <paramref name="viewModelType"/>, or throws naming the missing type.</summary>
    public Type GetViewType(Type viewModelType)
        => TryGetViewType(viewModelType, out var viewType)
            ? viewType
            : throw new InvalidOperationException(
                $"No view is registered for view model '{viewModelType.FullName}'. Map it in its feature's " +
                $"{nameof(IViewModule)}.{nameof(IViewModule.ViewModelToView)}.");

    /// <summary>
    /// Describes why <paramref name="viewType"/> cannot be instantiated by the view locator, or returns
    /// null when it can (a concrete <see cref="Control"/> with a public parameterless constructor).
    /// </summary>
    public static string? DescribeUnconstructibleView(Type viewType)
    {
        ArgumentNullException.ThrowIfNull(viewType);

        if (!typeof(Control).IsAssignableFrom(viewType))
            return $"'{viewType.FullName}' is not an Avalonia {nameof(Control)}.";
        if (viewType.IsAbstract || viewType.ContainsGenericParameters)
            return $"'{viewType.FullName}' is abstract or an open generic.";
        if (viewType.GetConstructor(Type.EmptyTypes) is null)
            return $"'{viewType.FullName}' has no public parameterless constructor.";
        return null;
    }
}
