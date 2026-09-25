using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Todo;

namespace MyWorkHub.UI.Features.Todo;

/// <summary>Maps the Todo page view model to its view.</summary>
public sealed class TodoViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(TodoViewModel)] = typeof(TodoView),
    };
}
