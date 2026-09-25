using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.PullRequests;

namespace MyWorkHub.UI.Features.PullRequests;

/// <summary>Maps the Pull requests page view model to its view.</summary>
public sealed class PullRequestsViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(PullRequestsViewModel)] = typeof(PullRequestsView),
    };
}
