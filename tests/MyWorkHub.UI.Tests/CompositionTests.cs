using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.DependencyInjection;
using MyWorkHub.Presentation.Features.Dashboard;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Features.Dashboard;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

public sealed class CompositionTests
{
    private static ServiceCollection ComposeWithSampleModule()
    {
        var services = new ServiceCollection();
        services.AddUi([
            typeof(PresentationServiceCollectionExtensions).Assembly,
            typeof(UiServiceCollectionExtensions).Assembly,
            typeof(CompositionTests).Assembly,
        ]);
        return services;
    }

    // --- ViewRegistry ---------------------------------------------------------------

    [Fact]
    public void Should_merge_the_maps_of_every_view_module()
    {
        var registry = new ViewRegistry([new DashboardViewModule(), new SampleViewModule()]);

        Assert.Equal(typeof(DashboardView), registry.GetViewType(typeof(DashboardViewModel)));
        Assert.Equal(typeof(SampleView), registry.GetViewType(typeof(SamplePageViewModel)));
    }

    [Fact]
    public void Should_throw_naming_the_view_model_when_two_modules_map_it()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ViewRegistry([
            new SampleViewModule(),
            new FixedViewModule(new Dictionary<Type, Type> { [typeof(SamplePageViewModel)] = typeof(DashboardView) }),
        ]));

        Assert.Contains(typeof(SamplePageViewModel).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_throw_naming_the_view_model_when_no_view_is_registered()
    {
        var registry = new ViewRegistry([]);

        var ex = Assert.Throws<InvalidOperationException>(() => registry.GetViewType(typeof(OrphanPageViewModel)));

        Assert.Contains(typeof(OrphanPageViewModel).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_register_the_dashboard_mapping_in_the_default_composition()
    {
        var registry = new ServiceCollection().AddUi().BuildServiceProvider().GetRequiredService<ViewRegistry>();

        Assert.Equal(typeof(DashboardView), registry.GetViewType(typeof(DashboardViewModel)));
    }

    [Fact]
    public void Should_describe_a_view_type_that_is_not_a_control()
    {
        Assert.NotNull(ViewRegistry.DescribeUnconstructibleView(typeof(string)));
        Assert.Null(ViewRegistry.DescribeUnconstructibleView(typeof(SampleView)));
    }

    // --- ViewLocator ----------------------------------------------------------------

    [Fact]
    public void Should_throw_naming_the_view_model_when_the_locator_builds_an_unmapped_page()
    {
        var locator = new ViewLocator(new ViewRegistry([]));

        var ex = Assert.Throws<InvalidOperationException>(() => locator.Build(new OrphanPageViewModel()));

        Assert.Contains(typeof(OrphanPageViewModel).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_build_nothing_when_the_locator_gets_null()
    {
        Assert.Null(new ViewLocator(new ViewRegistry([])).Build(null));
    }

    [Fact]
    public void Should_match_only_view_models()
    {
        var locator = new ViewLocator(new ViewRegistry([]));

        Assert.True(locator.Match(new PlainPageViewModel()));
        Assert.False(locator.Match("not a view model"));
    }

    // --- ShellCompositionValidator ---------------------------------------------------

    [Fact]
    public void Should_pass_validation_for_the_default_composition()
    {
        var services = new ServiceCollection();
        services.AddUi();

        ShellCompositionValidator.Validate(services, services.BuildServiceProvider());
    }

    [Fact]
    public void Should_pass_validation_when_a_complete_feature_module_is_added()
    {
        var services = ComposeWithSampleModule();

        ShellCompositionValidator.Validate(services, services.BuildServiceProvider());
    }

    [Fact]
    public void Should_fail_validation_naming_the_page_when_a_registered_page_has_no_view()
    {
        var services = new ServiceCollection();
        services.AddUi();
        services.AddTransient<OrphanPageViewModel>();

        var ex = Assert.Throws<InvalidOperationException>(
            () => ShellCompositionValidator.Validate(services, services.BuildServiceProvider()));

        Assert.Contains(typeof(OrphanPageViewModel).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_fail_validation_naming_the_page_when_a_menu_entry_targets_an_unregistered_page()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ViewRegistry([
            new FixedViewModule(new Dictionary<Type, Type> { [typeof(OrphanPageViewModel)] = typeof(SampleView) }),
        ]));
        services.AddSingleton<IReadOnlyList<NavigationItem>>([new NavigationItem("Orphan", "O", typeof(OrphanPageViewModel))]);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ShellCompositionValidator.Validate(services, services.BuildServiceProvider()));

        Assert.Contains(typeof(OrphanPageViewModel).FullName!, ex.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be resolved", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_fail_validation_when_the_mapped_view_cannot_be_constructed()
    {
        var services = new ServiceCollection();
        services.AddTransient<OrphanPageViewModel>();
        services.AddSingleton(new ViewRegistry([
            new FixedViewModule(new Dictionary<Type, Type> { [typeof(OrphanPageViewModel)] = typeof(string) }),
        ]));
        services.AddSingleton<IReadOnlyList<NavigationItem>>([]);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ShellCompositionValidator.Validate(services, services.BuildServiceProvider()));

        Assert.Contains("not an Avalonia Control", ex.Message, StringComparison.Ordinal);
    }
}
