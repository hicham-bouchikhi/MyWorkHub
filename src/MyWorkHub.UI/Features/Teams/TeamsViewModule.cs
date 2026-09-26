using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Teams;

namespace MyWorkHub.UI.Features.Teams;

/// <summary>Maps the Teams page view model to its view.</summary>
public sealed class TeamsViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(TeamsViewModel)] = typeof(TeamsView),
    };
}
