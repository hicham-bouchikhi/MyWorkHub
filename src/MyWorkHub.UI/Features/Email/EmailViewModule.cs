using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Email;

namespace MyWorkHub.UI.Features.Email;

/// <summary>Maps the Email page view model to its view.</summary>
public sealed class EmailViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(EmailViewModel)] = typeof(EmailView),
    };
}
