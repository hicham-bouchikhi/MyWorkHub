using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.DependencyInjection;
using MyWorkHub.Presentation.Features.Dashboard;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

public sealed class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateDefaultShell()
        => new ServiceCollection().AddUi().BuildServiceProvider().GetRequiredService<MainWindowViewModel>();

    private static MainWindowViewModel CreateShellWithSampleModule()
        => new ServiceCollection()
            .AddUi([
                typeof(PresentationServiceCollectionExtensions).Assembly,
                typeof(UiServiceCollectionExtensions).Assembly,
                typeof(MainWindowViewModelTests).Assembly,
            ])
            .BuildServiceProvider()
            .GetRequiredService<MainWindowViewModel>();

    [Fact]
    public void Should_list_the_dashboard_first_then_the_installed_features_in_menu_order()
    {
        var vm = CreateDefaultShell();

        Assert.Equal(["Dashboard", "Todo"], vm.NavigationItems.Select(i => i.Label));
    }

    [Fact]
    public void Should_land_on_the_dashboard_at_startup()
    {
        var vm = CreateDefaultShell();

        Assert.Equal("Dashboard", vm.SelectedItem?.Label);
        Assert.IsType<DashboardViewModel>(vm.Navigation.CurrentPage);
    }

    [Fact]
    public void Should_add_a_sidebar_entry_when_a_new_presentation_module_is_discovered()
    {
        var vm = CreateShellWithSampleModule();

        // Sample's Order (10) sorts it ahead of Todo's (70).
        Assert.Equal(["Dashboard", "Sample", "Todo"], vm.NavigationItems.Select(i => i.Label));
    }

    [Fact]
    public void Should_navigate_to_the_page_when_a_sidebar_entry_is_selected()
    {
        var vm = CreateShellWithSampleModule();

        vm.SelectedItem = vm.NavigationItems.Single(i => i.Label == "Sample");

        Assert.IsType<SamplePageViewModel>(vm.Navigation.CurrentPage);
    }

    [Fact]
    public void Should_sync_the_selected_entry_when_navigation_happens_elsewhere()
    {
        var vm = CreateShellWithSampleModule();

        vm.Navigation.NavigateTo(new NavigationTarget(typeof(SamplePageViewModel)));

        Assert.Equal("Sample", vm.SelectedItem?.Label);
    }

    [Fact]
    public void Should_show_no_page_when_the_menu_is_empty()
    {
        var navigation = new FakeNavigationService();

        var vm = new MainWindowViewModel(navigation, new NotificationCenterViewModel(navigation), []);

        Assert.Null(vm.SelectedItem);
        Assert.Empty(navigation.Requests);
    }

    [Fact]
    public void Should_flip_expansion_and_width_when_toggling_the_sidebar()
    {
        var vm = CreateDefaultShell();
        var expandedWidth = vm.SidebarWidth;

        vm.ToggleSidebarCommand.Execute(null);

        Assert.False(vm.IsSidebarExpanded);
        Assert.NotEqual(expandedWidth, vm.SidebarWidth);
    }

    [Fact]
    public void Should_register_a_singleton_navigation_service()
    {
        var provider = new ServiceCollection().AddUi().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<INavigationService>(), provider.GetRequiredService<INavigationService>());
    }
}
