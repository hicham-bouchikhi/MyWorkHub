using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Dev;

namespace MyWorkHub.UI.Features.Dev;

/// <summary>Maps the Developer page view model to its view.</summary>
public sealed class DevViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(DevViewModel)] = typeof(DevView),
    };
}
