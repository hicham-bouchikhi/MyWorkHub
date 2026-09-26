using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.UI.Tests.Features.GraphAuth;
using MyWorkHub.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests.Features.Email;

/// <summary>
/// End-to-end deep-link path through the real composition, as for Todo:
/// NavigationTarget(EmailViewModel, messageId) → NavigationService → EmailViewModel.FocusElement → row selected + highlighted.
/// </summary>
public sealed class EmailDeepLinkTests
{
    private static ServiceProvider Compose(params EmailItem[] emails)
    {
        var connection = new FakeGraphConnection();
        var services = new ServiceCollection();
        services.AddSingleton<IGraphConnectionService>(connection);
        services.AddSingleton<IEmailService>(new FakeEmailService(connection, emails));
        services.AddUi();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_land_on_the_email_page_with_the_message_selected_when_navigating_before_first_load()
    {
        using var provider = Compose(FakeEmailService.Item("other"), FakeEmailService.Item("target"));
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateTo(EmailViewModel.TargetFor("target"));
        var page = Assert.IsType<EmailViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null); // what EmailView does when it appears

        Assert.Equal("target", page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }

    [Fact]
    public async Task Should_focus_the_message_on_the_cached_page_when_navigating_back_with_a_target()
    {
        using var provider = Compose(FakeEmailService.Item("target"), FakeEmailService.Item("other"));
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.NavigateTo(new(typeof(EmailViewModel)));
        var page = Assert.IsType<EmailViewModel>(navigation.CurrentPage);
        await page.LoadCommand.ExecuteAsync(null);
        Assert.Null(page.SelectedItem);

        navigation.NavigateTo(EmailViewModel.TargetFor("target"));

        Assert.Same(page, navigation.CurrentPage);
        Assert.Equal("target", page.SelectedItem?.Id);
        Assert.Same(page.SelectedItem, page.HighlightedItem);
    }
}
