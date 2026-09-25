using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Dashboard;

namespace MyWorkHub.UI.Features.Dashboard;

/// <summary>Maps the Dashboard page view model to its view.</summary>
public sealed class DashboardViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(DashboardViewModel)] = typeof(DashboardView),
    };
}
