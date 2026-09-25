using System.ComponentModel;
using Avalonia.Controls;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

/// <summary>Records navigation requests for view-model tests.</summary>
internal sealed class FakeNavigationService : INavigationService
{
    public List<NavigationTarget> Requests { get; } = [];

    public ViewModelBase? CurrentPage { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(NavigationTarget target)
    {
        Requests.Add(target);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPage)));
    }
}

/// <summary>A plain page with no deep-link support.</summary>
public sealed class PlainPageViewModel : PageViewModel
{
    public PlainPageViewModel()
        : base("Plain")
    {
    }
}

/// <summary>A page that records every element it was asked to focus.</summary>
public sealed class DeepLinkPageViewModel : PageViewModel, IDeepLinkTarget
{
    public DeepLinkPageViewModel()
        : base("Deep link")
    {
    }

    public System.Collections.ObjectModel.Collection<string> FocusedElementIds { get; } = [];

    public void FocusElement(string elementId) => FocusedElementIds.Add(elementId);
}

/// <summary>A page that no view module maps — used to prove validation fails loudly.</summary>
public sealed class OrphanPageViewModel : PageViewModel
{
    public OrphanPageViewModel()
        : base("Orphan")
    {
    }
}

/// <summary>
/// Sample feature, discovered only when a test passes this test assembly to <c>AddUi</c>
/// (the default composition scans just the Presentation and UI assemblies).
/// </summary>
public sealed class SamplePageViewModel : PageViewModel
{
    public SamplePageViewModel()
        : base("Sample")
    {
    }
}

public sealed class SampleView : UserControl;

public sealed class SamplePresentationModule : IPresentationModule
{
    public NavigationItem? MenuEntry { get; } = new("Sample", "S", typeof(SamplePageViewModel), 10);

    public void RegisterServices(IServiceCollection services) => services.AddTransient<SamplePageViewModel>();
}

public sealed class SampleViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } =
        new Dictionary<Type, Type> { [typeof(SamplePageViewModel)] = typeof(SampleView) };
}

/// <summary>A view module with an arbitrary map. No parameterless constructor, so never auto-discovered.</summary>
internal sealed class FixedViewModule : IViewModule
{
    public FixedViewModule(IReadOnlyDictionary<Type, Type> map) => ViewModelToView = map;

    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; }
}
