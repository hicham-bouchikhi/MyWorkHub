using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.WorkItems;

namespace MyWorkHub.UI.Features.WorkItems;

/// <summary>Maps the Work items page view model to its view.</summary>
public sealed class WorkItemsViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(WorkItemsViewModel)] = typeof(WorkItemsView),
    };
}
