using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Core.Features.Calendar;
using MyWorkHub.Core.Features.Dashboard;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Core.Features.Todo;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Navigation;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Dashboard;

/// <summary>
/// Landing page showing at-a-glance summaries from all features: unread emails/Teams, pending PRs/work items,
/// todo progress, and upcoming calendar events. Loads stats on first display and refreshes when revisited.
/// Each stat tile is clickable and navigates to the corresponding feature page.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    private readonly IEmailService? _email;
    private readonly ITeamsService? _teams;
    private readonly IAzureDevOpsService? _azureDevOps;
    private readonly ITodoRepository? _todos;
    private readonly ICalendarService? _calendar;
    private readonly INavigationService _navigation;

    public DashboardViewModel(
        IEmailService? email = null,
        ITeamsService? teams = null,
        IAzureDevOpsService? azureDevOps = null,
        ITodoRepository? todos = null,
        ICalendarService? calendar = null,
        INavigationService? navigation = null)
        : base("Dashboard")
    {
        _email = email;
        _teams = teams;
        _azureDevOps = azureDevOps;
        _todos = todos;
        _calendar = calendar;
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private EmailStats? _emailStats;

    [ObservableProperty]
    private TeamsStats? _teamsStats;

    [ObservableProperty]
    private AzureDevOpsStats? _azureDevOpsStats;

    [ObservableProperty]
    private TodoStats? _todoStats;

    [ObservableProperty]
    private CalendarStats? _calendarStats;

    [RelayCommand]
    private async Task LoadStatsAsync(CancellationToken ct)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tasks = new List<Task>();

            if (_email is not null)
                tasks.Add(LoadEmailStatsAsync(ct));

            if (_teams is not null)
                tasks.Add(LoadTeamsStatsAsync(ct));

            if (_azureDevOps is not null)
                tasks.Add(LoadAzureDevOpsStatsAsync(ct));

            if (_todos is not null)
                tasks.Add(LoadTodoStatsAsync(ct));

            if (_calendar is not null)
                tasks.Add(LoadCalendarStatsAsync(ct));

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded: the command cancels a still-running execution when it is executed again (e.g. the
            // page is revisited mid-load). The newer run owns the page; rethrowing would crash the dispatcher.
            return;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Unable to load dashboard stats. Check your settings.";
            System.Diagnostics.Debug.WriteLine($"Dashboard load error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NavigateToEmail()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.Email.EmailViewModel)));

    [RelayCommand]
    private void NavigateToTeams()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.Teams.TeamsViewModel)));

    [RelayCommand]
    private void NavigateToPullRequests()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.PullRequests.PullRequestsViewModel)));

    [RelayCommand]
    private void NavigateToWorkItems()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.WorkItems.WorkItemsViewModel)));

    [RelayCommand]
    private void NavigateToTodo()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.Todo.TodoViewModel)));

    [RelayCommand]
    private void NavigateToCalendar()
        => _navigation.NavigateTo(new NavigationTarget(typeof(Presentation.Features.Calendar.CalendarViewModel)));

    private async Task LoadEmailStatsAsync(CancellationToken ct)
    {
        try
        {
            var emails = await _email!.GetRecentEmailsAsync(ct);
            var unreadCount = emails.Count(e => !e.IsRead);
            EmailStats = new(unreadCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Email stats error: {ex.Message}");
        }
    }

    private async Task LoadTeamsStatsAsync(CancellationToken ct)
    {
        try
        {
            var chats = await _teams!.GetRecentChatsAsync(ct);
            var unreadCount = chats.Count(c => c.IsUnread);
            TeamsStats = new(unreadCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Teams stats error: {ex.Message}");
        }
    }

    private async Task LoadAzureDevOpsStatsAsync(CancellationToken ct)
    {
        try
        {
            var prs = await _azureDevOps!.GetMyPullRequestsAsync(ct);
            var prCount = prs.Count;
            var pendingReviewCount = prs.Count(pr =>
                pr.Role == PullRequestRole.REVIEWER && pr.MyVote == PullRequestVote.NONE);

            var workItems = await _azureDevOps!.GetMyWorkItemsAsync(ct);
            var workItemCount = workItems.Count;

            AzureDevOpsStats = new(prCount, pendingReviewCount, workItemCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Azure DevOps stats error: {ex.Message}");
        }
    }

    private async Task LoadTodoStatsAsync(CancellationToken ct)
    {
        try
        {
            var todos = await _todos!.GetAllAsync(ct);
            var totalCount = todos.Count;
            var completedCount = todos.Count(t => t.IsCompleted);
            TodoStats = new(totalCount, completedCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Todo stats error: {ex.Message}");
        }
    }

    private async Task LoadCalendarStatsAsync(CancellationToken ct)
    {
        try
        {
            var events = await _calendar!.GetUpcomingEventsAsync(7, ct);
            var upcomingCount = events.Count;
            var nextEventTime = events.Count > 0 ? events[0].Start : (DateTime?)null;
            CalendarStats = new(upcomingCount, nextEventTime);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Calendar stats error: {ex.Message}");
        }
    }
}
