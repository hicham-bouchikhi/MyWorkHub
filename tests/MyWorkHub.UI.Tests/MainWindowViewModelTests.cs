using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

public sealed class MainWindowViewModelTests
{
    private static readonly string[] _expectedPageOrder =
        ["Dashboard", "Email", "Pull Requests", "Work Items", "Teams", "Todo", "Remote Work", "Automations", "Settings"];

    private static MainWindowViewModel CreateViewModel()
    {
        var provider = new ServiceCollection().AddUi().BuildServiceProvider();
        return provider.GetRequiredService<MainWindowViewModel>();
    }

    [Fact]
    public void Sidebar_lists_the_nine_pages_in_order()
    {
        var vm = CreateViewModel();

        var labels = vm.NavigationItems.Select(i => i.Label).ToArray();

        Assert.Equal(_expectedPageOrder, labels);
    }

    [Fact]
    public void Defaults_to_the_Dashboard_page()
    {
        var vm = CreateViewModel();

        Assert.Equal("Dashboard", vm.SelectedItem?.Label);
        Assert.IsType<DashboardViewModel>(vm.Navigation.CurrentPage);
    }

    [Fact]
    public void Selecting_a_nav_item_navigates_to_its_page()
    {
        var vm = CreateViewModel();
        var teams = vm.NavigationItems.Single(i => i.Label == "Teams");

        vm.SelectedItem = teams;

        Assert.IsType<TeamsViewModel>(vm.Navigation.CurrentPage);
    }

    [Fact]
    public void Toggling_the_sidebar_flips_expansion_and_width()
    {
        var vm = CreateViewModel();
        var expandedWidth = vm.SidebarWidth;

        vm.ToggleSidebarCommand.Execute(null);

        Assert.False(vm.IsSidebarExpanded);
        Assert.NotEqual(expandedWidth, vm.SidebarWidth);
    }
}
