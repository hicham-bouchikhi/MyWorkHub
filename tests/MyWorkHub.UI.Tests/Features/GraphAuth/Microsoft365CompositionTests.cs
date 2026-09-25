using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Calendar;
using MyWorkHub.Presentation.Features.Email;
using MyWorkHub.Presentation.Features.Teams;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Features.Calendar;
using MyWorkHub.UI.Features.Email;
using MyWorkHub.UI.Features.Teams;
using Microsoft.Extensions.DependencyInjection;

namespace MyWorkHub.UI.Tests.Features.GraphAuth;

/// <summary>The three Microsoft 365 pages plug into the shell purely through their modules.</summary>
public sealed class Microsoft365CompositionTests
{
    [Theory]
    [InlineData(typeof(EmailViewModel), typeof(EmailView))]
    [InlineData(typeof(CalendarViewModel), typeof(CalendarView))]
    [InlineData(typeof(TeamsViewModel), typeof(TeamsView))]
    public void Should_contribute_a_sidebar_entry_and_a_view_in_the_default_composition(Type viewModelType, Type viewType)
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        Assert.Contains(provider.GetRequiredService<IReadOnlyList<NavigationItem>>(), i => i.ViewModelType == viewModelType);
        Assert.Equal(viewType, provider.GetRequiredService<ViewRegistry>().GetViewType(viewModelType));
        ShellCompositionValidator.Validate(services, provider);
    }

    [Fact]
    public void Should_list_the_microsoft_365_pages_after_the_dashboard_and_before_todo()
    {
        var services = new ServiceCollection();
        services.AddUi();
        using var provider = services.BuildServiceProvider();

        var labels = provider.GetRequiredService<IReadOnlyList<NavigationItem>>().Select(i => i.Label).ToList();

        // Only the Microsoft 365 pages' placement is asserted here; other features may sit in between.
        Assert.Equal(["Dashboard", "Email", "Calendar", "Teams"], labels.Take(4));
        Assert.True(labels.IndexOf("Teams") < labels.IndexOf("Todo"));
    }
}
