using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Navigation;
using MyWorkHub.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

public sealed class NavigationServiceTests
{
    private static INavigationService CreateNavigation()
    {
        var provider = new ServiceCollection().AddUi().BuildServiceProvider();
        return provider.GetRequiredService<INavigationService>();
    }

    [Fact]
    public void NavigateTo_resolves_the_page_and_sets_it_as_current()
    {
        var navigation = CreateNavigation();

        navigation.NavigateTo(typeof(EmailViewModel));

        Assert.IsType<EmailViewModel>(navigation.CurrentPage);
    }

    [Fact]
    public void NavigateTo_raises_property_changed_for_CurrentPage()
    {
        var navigation = CreateNavigation();
        var raised = false;
        navigation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(INavigationService.CurrentPage))
            {
                raised = true;
            }
        };

        navigation.NavigateTo(typeof(TodoViewModel));

        Assert.True(raised);
    }

    [Fact]
    public void NavigateTo_reuses_the_same_cached_page_instance_on_return()
    {
        // Page state (e.g. an in-progress PR review) must survive navigating away and back,
        // so the navigation service caches one instance per page type.
        var navigation = CreateNavigation();

        navigation.NavigateTo(typeof(SettingsViewModel));
        var first = navigation.CurrentPage;
        navigation.NavigateTo(typeof(EmailViewModel));
        navigation.NavigateTo(typeof(SettingsViewModel));
        var second = navigation.CurrentPage;

        Assert.Same(first, second);
    }

    [Fact]
    public void NavigateTo_null_type_throws()
    {
        var navigation = CreateNavigation();

        Assert.Throws<ArgumentNullException>(() => navigation.NavigateTo(null!));
    }
}
