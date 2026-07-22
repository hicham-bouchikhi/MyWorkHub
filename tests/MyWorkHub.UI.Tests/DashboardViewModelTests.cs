using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using MyWorkHub.Core.Models;
using MyWorkHub.UI.Navigation;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class DashboardViewModelTests
{
    private static readonly DateTime _fixedNow = new(2026, 6, 22, 9, 0, 0, DateTimeKind.Local);

    [Fact]
    public async Task Refresh_aggregates_counts_and_top_three_emails()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var email = new FakeEmailService(
            unreadCount: 7,
            important:
            [
                Email("a"), Email("b"), Email("c"), Email("d"),
            ]);
        var azdo = new FakeAzureDevOpsService(
            pullRequests: [Pr(1), Pr(2)],
            workItems:
            [
                WorkItemDue(DateTime.Today),        // due today -> counts
                WorkItemDue(DateTime.Today.AddDays(-3)), // overdue -> counts
                WorkItemDue(DateTime.Today.AddDays(5)),  // future -> does not count
            ]);
        var todo = new FakeTodoRepository(
        [
            Todo(today, completed: false),               // due today -> counts
            Todo(today.AddDays(-1), completed: false),    // overdue -> counts
            Todo(today, completed: true),                 // completed -> does not count
            Todo(today.AddDays(2), completed: false),     // future -> does not count
        ]);

        var vm = new DashboardViewModel(new StubNavigationService(), email, azdo, todo: todo);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(7, vm.UnreadEmailCount);
        Assert.Equal(3, vm.TopEmails.Count);
        Assert.True(vm.HasImportantEmails);
        Assert.Equal(2, vm.PullRequestCount);
        Assert.Equal(3, vm.WorkItemCount);          // total assigned in the sprint
        Assert.Equal(2, vm.WorkItemsAttentionCount); // of which due-today/overdue
        Assert.Equal(2, vm.TodoDueTodayCount);
    }

    [Fact]
    public async Task Refresh_uses_optional_Teams_service_when_present()
    {
        var vm = new DashboardViewModel(new StubNavigationService(), teams: new FakeTeamsService(unreadCount: 4));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(4, vm.TeamsUnreadCount);
    }

    [Fact]
    public async Task Refresh_keeps_dashboard_healthy_when_Teams_throws()
    {
        var vm = new DashboardViewModel(new StubNavigationService(), teams: new ThrowingTeamsService());

        // A throwing Teams service must not bubble up; the refresh completes and the count is 0.
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.TeamsUnreadCount);
    }

    [Fact]
    public async Task Refresh_marks_offline_when_a_core_source_fails()
    {
        var vm = new DashboardViewModel(new StubNavigationService(), email: new ThrowingEmailService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsOffline);
    }

    [Fact]
    public async Task Refresh_offline_message_names_the_failed_sources()
    {
        var vm = new DashboardViewModel(new StubNavigationService(), email: new ThrowingEmailService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("Email", vm.OfflineMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure DevOps", vm.OfflineMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_offline_message_is_empty_when_all_services_succeed()
    {
        var vm = new DashboardViewModel(new StubNavigationService(), email: new FakeEmailService(0, []));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsOffline);
        Assert.Equal("", vm.OfflineMessage);
    }

    [Fact]
    public async Task Refresh_with_no_services_leaves_widgets_at_zero()
    {
        var vm = new DashboardViewModel(new StubNavigationService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.UnreadEmailCount);
        Assert.Equal(0, vm.TeamsUnreadCount);
        Assert.Equal(0, vm.PullRequestCount);
        Assert.Empty(vm.TopEmails);
    }

    [Fact]
    public async Task Refresh_with_azdo_not_configured_shows_zero_and_stays_online()
    {
        var settings = new FakeAzureDevOpsSettingsService(hasPat: false, projects: []);
        var vm = new DashboardViewModel(
            new StubNavigationService(),
            azureDevOps: new FakeAzureDevOpsService([Pr(1), Pr(2)], []),
            azdoSettings: settings);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.PullRequestCount);
        Assert.False(vm.IsOffline);
    }

    [Fact]
    public void Widget_commands_navigate_to_their_pages()
    {
        var navigation = new StubNavigationService();
        var vm = new DashboardViewModel(navigation);

        vm.OpenEmailCommand.Execute(null);
        Assert.Equal(typeof(EmailViewModel), navigation.LastRequested);

        vm.OpenPullRequestsCommand.Execute(null);
        Assert.Equal(typeof(PullRequestsViewModel), navigation.LastRequested);

        vm.OpenWorkItemsCommand.Execute(null);
        Assert.Equal(typeof(WorkItemsViewModel), navigation.LastRequested);

        vm.OpenTeamsCommand.Execute(null);
        Assert.Equal(typeof(TeamsViewModel), navigation.LastRequested);

        vm.OpenTodoCommand.Execute(null);
        Assert.Equal(typeof(TodoViewModel), navigation.LastRequested);

        vm.OpenAutomationsCommand.Execute(null);
        Assert.Equal(typeof(AutomationsViewModel), navigation.LastRequested);
    }

    private static EmailItem Email(string id) => new(id, "sender@x", "Subject " + id, "preview", _fixedNow, false);

    private static PullRequestItem Pr(int id) => new(id, "PR " + id, "author", "repo", _fixedNow, "None", "https://x");

    private static WorkItem WorkItemDue(DateTime due) =>
        new(1, "WI", "Task", "Active", "2", due, "https://x");

    private static TodoItem Todo(DateOnly due, bool completed) =>
        new() { Id = Guid.NewGuid(), Title = "t", DueDate = due, IsCompleted = completed };

    private sealed class StubNavigationService : INavigationService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public ViewModelBase? CurrentPage { get; private set; }

        public Type? LastRequested { get; private set; }

        public void NavigateTo(Type viewModelType)
        {
            LastRequested = viewModelType;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CurrentPage)));
        }
    }

    private sealed class FakeEmailService(int unreadCount, IReadOnlyList<EmailItem> important) : IEmailService
    {
        public Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default) =>
            Task.FromResult(important);

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default) => Task.FromResult(unreadCount);

        public Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MailFolder>>([]);

        public Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(string.Empty);

        public Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingEmailService : IEmailService
    {
        public Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");

        public Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAzureDevOpsService(IReadOnlyList<PullRequestItem> pullRequests, IReadOnlyList<WorkItem> workItems) : IAzureDevOpsService
    {
        public Task<IReadOnlyList<PullRequestItem>> GetPullRequestsForReviewAsync(CancellationToken ct = default) =>
            Task.FromResult(pullRequests);

        public Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default) =>
            Task.FromResult(workItems);

        public Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(IReadOnlyList<WorkItem> workItems, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkItemMention>>([]);

        public Task<WorkItemDetails?> GetWorkItemDetailsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<WorkItemDetails?>(null);

        public Task UpdateWorkItemAsync(int id, string? description, string? acceptanceCriteria, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeTeamsService(int unreadCount) : ITeamsService
    {
        public Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TeamsChatItem>>([]);

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default) => Task.FromResult(unreadCount);
    }

    private sealed class ThrowingTeamsService : ITeamsService
    {
        public Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("teams down");

        public Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("teams down");
    }

    private sealed class FakeTodoRepository(IReadOnlyList<TodoItem> items) : ITodoRepository
    {
        public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(items);

        public Task AddAsync(TodoItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(TodoItem item, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
