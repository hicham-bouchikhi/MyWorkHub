using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Automations;

namespace MyWorkHub.UI.Features.Automations;

/// <summary>Maps the Automations page view model to its view.</summary>
public sealed class AutomationsViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(AutomationsViewModel)] = typeof(AutomationsView),
    };
}
