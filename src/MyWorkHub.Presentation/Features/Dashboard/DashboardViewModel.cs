using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Dashboard;

/// <summary>
/// Landing page (first sidebar entry). No at-a-glance widgets yet — it only points to the sidebar.
/// </summary>
public sealed class DashboardViewModel : PageViewModel
{
    public DashboardViewModel()
        : base("Dashboard")
    {
    }
}
