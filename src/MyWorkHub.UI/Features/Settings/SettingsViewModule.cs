using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Settings;

namespace MyWorkHub.UI.Features.Settings;

/// <summary>Maps the Settings page view model to its view.</summary>
public sealed class SettingsViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(SettingsViewModel)] = typeof(SettingsView),
    };
}
