using MyWorkHub.Core.Modules;
using MyWorkHub.Presentation.Features.Calendar;

namespace MyWorkHub.UI.Features.Calendar;

/// <summary>Maps the Calendar page view model to its view.</summary>
public sealed class CalendarViewModule : IViewModule
{
    public IReadOnlyDictionary<Type, Type> ViewModelToView { get; } = new Dictionary<Type, Type>
    {
        [typeof(CalendarViewModel)] = typeof(CalendarView),
    };
}
