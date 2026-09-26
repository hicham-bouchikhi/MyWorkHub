using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests;

public sealed class NavigationServiceTests
{
    private static NavigationService CreateNavigation()
    {
        var provider = new ServiceCollection()
            .AddTransient<PlainPageViewModel>()
            .AddTransient<DeepLinkPageViewModel>()
            .AddTransient<NotAPage>()
            .BuildServiceProvider();
        return new NavigationService(provider);
    }

    [Fact]
    public void Should_make_the_resolved_page_current_when_navigating()
    {
        var navigation = CreateNavigation();

        navigation.NavigateTo(new NavigationTarget(typeof(PlainPageViewModel)));

        Assert.IsType<PlainPageViewModel>(navigation.CurrentPage);
    }

    [Fact]
    public void Should_raise_property_changed_for_current_page_when_navigating()
    {
        var navigation = CreateNavigation();
        var raised = false;
        navigation.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(INavigationService.CurrentPage);

        navigation.NavigateTo(new NavigationTarget(typeof(PlainPageViewModel)));

        Assert.True(raised);
    }

    [Fact]
    public void Should_reuse_the_cached_page_instance_when_returning_to_a_page()
    {
        // Page state must survive navigating away and back, so one instance is cached per page type.
        var navigation = CreateNavigation();

        navigation.NavigateTo(new NavigationTarget(typeof(PlainPageViewModel)));
        var first = navigation.CurrentPage;
        navigation.NavigateTo(new NavigationTarget(typeof(DeepLinkPageViewModel)));
        navigation.NavigateTo(new NavigationTarget(typeof(PlainPageViewModel)));

        Assert.Same(first, navigation.CurrentPage);
    }

    [Fact]
    public void Should_focus_the_element_when_the_target_names_one_and_the_page_is_a_deep_link_target()
    {
        var navigation = CreateNavigation();

        navigation.NavigateTo(new NavigationTarget(typeof(DeepLinkPageViewModel), "work-item-1234"));

        var page = Assert.IsType<DeepLinkPageViewModel>(navigation.CurrentPage);
        Assert.Equal(["work-item-1234"], page.FocusedElementIds);
    }

    [Fact]
    public void Should_focus_on_the_cached_page_when_deep_linking_to_an_already_visited_page()
    {
        var navigation = CreateNavigation();
        navigation.NavigateTo(new NavigationTarget(typeof(DeepLinkPageViewModel)));
        var page = Assert.IsType<DeepLinkPageViewModel>(navigation.CurrentPage);

        navigation.NavigateTo(new NavigationTarget(typeof(DeepLinkPageViewModel), "email-7"));

        Assert.Same(page, navigation.CurrentPage);
        Assert.Equal(["email-7"], page.FocusedElementIds);
    }

    [Fact]
    public void Should_not_focus_anything_when_the_target_has_no_element_id()
    {
        var navigation = CreateNavigation();

        navigation.NavigateTo(new NavigationTarget(typeof(DeepLinkPageViewModel)));

        var page = Assert.IsType<DeepLinkPageViewModel>(navigation.CurrentPage);
        Assert.Empty(page.FocusedElementIds);
    }

    [Fact]
    public void Should_still_navigate_when_an_element_id_targets_a_page_without_deep_link_support()
    {
        var navigation = CreateNavigation();

        navigation.NavigateTo(new NavigationTarget(typeof(PlainPageViewModel), "ignored"));

        Assert.IsType<PlainPageViewModel>(navigation.CurrentPage);
    }

    [Fact]
    public void Should_throw_when_the_target_type_is_not_a_view_model()
    {
        var navigation = CreateNavigation();

        var ex = Assert.Throws<InvalidOperationException>(() => navigation.NavigateTo(new NavigationTarget(typeof(NotAPage))));

        Assert.Contains(typeof(NotAPage).FullName!, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_throw_when_the_target_is_null()
    {
        var navigation = CreateNavigation();

        Assert.Throws<ArgumentNullException>(() => navigation.NavigateTo(null!));
    }

    internal sealed class NotAPage;
}
