using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.Composition;

namespace MyWorkHub.UI;

/// <summary>
/// Builds the view for a view model by looking it up in the <see cref="ViewRegistry"/> merged from
/// every <see cref="Core.Modules.IViewModule"/>. An unmapped view model is a composition bug and
/// throws (naming the type) rather than rendering a placeholder; startup validation normally
/// catches it first.
/// </summary>
[RequiresUnreferencedCode("Instantiates registered view types via reflection.")]
public sealed class ViewLocator : IDataTemplate
{
    private readonly ViewRegistry _registry;

    public ViewLocator(ViewRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewType = _registry.GetViewType(param.GetType());
        return (Control)Activator.CreateInstance(viewType)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
