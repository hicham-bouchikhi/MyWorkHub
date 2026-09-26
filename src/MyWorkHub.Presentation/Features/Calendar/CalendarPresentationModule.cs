using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Modules;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Presentation.Features.Calendar;

/// <summary>Registers the Calendar page and its sidebar entry.</summary>
public sealed class CalendarPresentationModule : IPresentationModule
{
    private const int MENU_ORDER = 20;

    public NavigationItem? MenuEntry { get; } =
        new("Calendar", "\U0001F4C5", typeof(CalendarViewModel), MENU_ORDER);

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<CalendarViewModel>();
    }
}
