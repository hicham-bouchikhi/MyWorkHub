using Microsoft.Extensions.DependencyInjection;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Core.Navigation;
using MyWorkHub.Presentation.Features.Automations;
using MyWorkHub.UI.Composition;
using MyWorkHub.UI.DependencyInjection;
using MyWorkHub.UI.Features.Automations;

namespace MyWorkHub.UI.Tests.Features.Automations;

public sealed class AutomationsViewModelTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 25, 8, 30, 0, TimeSpan.Zero);

    private readonly FakeScheduler _scheduler = new();
    private readonly FakeRunLog _runLog = new();
    private readonly FakeCredentials _credentials = new();
    private readonly FakePlans _plans = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AutomationsViewModel ViewModel() => new(_scheduler, _runLog, _credentials, _plans, new UtcClock());

    [Fact]
    public async Task Should_show_each_automation_with_its_schedule_and_next_run()
    {
        _scheduler.Schedules.Add(new AutomationSchedule(AutomationCatalog.TransportReimbursement, "0 0 9 1 * ?", new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), null));
        _scheduler.Schedules.Add(new AutomationSchedule(AutomationCatalog.RemoteWorkSync, null, null, "Not scheduled — disabled."));
        var vm = ViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Automations.Count);
        Assert.Equal("Scheduled (cron: 0 0 9 1 * ?)", vm.Automations[0].ScheduleText);
        Assert.Equal("Next run: 2026-10-01 09:00", vm.Automations[0].NextRunText);
        Assert.Equal("Not scheduled — disabled.", vm.Automations[1].ScheduleText);
        Assert.Equal("", vm.Automations[1].NextRunText);
    }

    [Fact]
    public async Task Should_list_recent_runs_with_their_per_step_outcomes()
    {
        _runLog.Runs.Add(new AutomationRunRecord(
            Guid.NewGuid(),
            AutomationCatalog.RemoteWorkSync.Id,
            _now.UtcDateTime,
            _now.UtcDateTime.AddMinutes(1),
            AutomationRunStatus.PARTIAL,
            "PeopleNet: login rejected",
            [
                new AutomationStepResult("Outlook calendar", AutomationStepStatus.SUCCESS),
                new AutomationStepResult("PeopleNet", AutomationStepStatus.FAILED, "login rejected"),
                new AutomationStepResult("mwork", AutomationStepStatus.SUCCESS),
            ]));
        var vm = ViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        var run = Assert.Single(vm.Runs);
        Assert.Equal("Remote-work sync", run.Name);
        Assert.Equal("Partly failed", run.StatusText);
        Assert.True(run.IsPartial);
        Assert.Equal("2026-09-25 08:30", run.StartedText);
        Assert.Equal(["✔", "✖", "✔"], run.Steps.Select(s => s.Glyph));
        Assert.Equal("login rejected", run.Steps[1].Message);
    }

    [Fact]
    public async Task Should_queue_a_run_and_say_so_when_run_now_is_pressed()
    {
        _scheduler.Schedules.Add(new AutomationSchedule(AutomationCatalog.RemoteWorkSync, null, null, null));
        var vm = ViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RunNowCommand.ExecuteAsync(vm.Automations[0]);

        Assert.Equal([AutomationCatalog.RemoteWorkSync.Id], _scheduler.RunNowRequests);
        Assert.StartsWith("Started", vm.Automations[0].RunNowStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_report_on_the_card_when_a_run_cannot_be_queued()
    {
        _scheduler.Schedules.Add(new AutomationSchedule(AutomationCatalog.RemoteWorkSync, null, null, null));
        _scheduler.RunNowFailure = new InvalidOperationException("scheduler stopped");
        var vm = ViewModel();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RunNowCommand.ExecuteAsync(vm.Automations[0]);

        Assert.Equal("Could not start: scheduler stopped", vm.Automations[0].RunNowStatus);
    }

    [Fact]
    public async Task Should_show_an_error_instead_of_throwing_when_the_history_cannot_be_read()
    {
        _runLog.Failure = new InvalidOperationException("database locked");
        var vm = ViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("database locked", vm.ErrorMessage, StringComparison.Ordinal);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Should_load_and_save_the_current_month_plan()
    {
        _plans.Saved = RemoteWorkPlan.Create(2026, 9, [2]);
        var vm = ViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal("September 2026", vm.PlanMonthLabel);
        Assert.Equal("2", vm.PlanDaysText);

        vm.PlanDaysText = "16 9, 9";
        await vm.SavePlanCommand.ExecuteAsync(null);

        Assert.Equal([9, 16], _plans.Saved!.Days.Select(d => d.Day));
        Assert.Equal((2026, 9), (_plans.Saved.Year, _plans.Saved.Month));
        Assert.Equal("9, 16", vm.PlanDaysText);
        Assert.Equal("Saved: 2 remote-work day(s).", vm.PlanMessage);
    }

    [Fact]
    public async Task Should_not_save_a_plan_with_an_invalid_day()
    {
        var vm = ViewModel();
        vm.PlanDaysText = "2, 31";

        await vm.SavePlanCommand.ExecuteAsync(null);

        Assert.Null(_plans.Saved);
        Assert.Contains("\"31\"", vm.PlanMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_offer_a_login_form_per_site_showing_which_logins_are_saved()
    {
        _credentials.Logins[AutomationSite.PEOPLENET] = new SiteCredentials("jdoe", "pw");

        var vm = ViewModel();

        Assert.Equal([AutomationSite.TCL, AutomationSite.PEOPLENET, AutomationSite.MWORK], vm.SiteLogins.Select(l => l.Site));
        Assert.Equal([false, true, false], vm.SiteLogins.Select(l => l.HasSavedLogin));
    }

    [Fact]
    public void Should_save_a_login_and_clear_the_typed_password()
    {
        var vm = ViewModel();
        var row = vm.SiteLogins.Single(l => l.Site == AutomationSite.MWORK);
        row.UserName = " jdoe ";
        row.Password = "s3cret";

        vm.SaveLoginCommand.Execute(row);

        Assert.Equal(new SiteCredentials("jdoe", "s3cret"), _credentials.Logins[AutomationSite.MWORK]);
        Assert.True(row.HasSavedLogin);
        Assert.Equal("", row.Password);
        Assert.Equal("", row.UserName);
    }

    [Fact]
    public void Should_not_save_an_incomplete_login()
    {
        var vm = ViewModel();
        var row = vm.SiteLogins[0];
        row.UserName = "jdoe";

        Assert.False(row.CanSave);
        vm.SaveLoginCommand.Execute(row);

        Assert.Empty(_credentials.Logins);
    }

    [Fact]
    public void Should_forget_a_saved_login()
    {
        _credentials.Logins[AutomationSite.TCL] = new SiteCredentials("jdoe", "pw");
        var vm = ViewModel();

        vm.ClearLoginCommand.Execute(vm.SiteLogins[0]);

        Assert.Empty(_credentials.Logins);
        Assert.False(vm.SiteLogins[0].HasSavedLogin);
    }

    [Fact]
    public async Task Should_degrade_to_a_notice_when_no_automation_services_are_registered()
    {
        var vm = new AutomationsViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.IsAvailable);
        Assert.False(vm.HasHistory);
        Assert.False(vm.CanManageLogins);
        Assert.False(vm.CanEditPlan);
        Assert.Empty(vm.SiteLogins);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void Should_contribute_a_sidebar_entry_and_a_view_in_the_default_composition()
    {
        using var provider = new ServiceCollection().AddUi().BuildServiceProvider();

        Assert.Contains(provider.GetRequiredService<IReadOnlyList<NavigationItem>>(), i => i.ViewModelType == typeof(AutomationsViewModel));
        Assert.Equal(typeof(AutomationsView), provider.GetRequiredService<ViewRegistry>().GetViewType(typeof(AutomationsViewModel)));
        Assert.IsType<AutomationsViewModel>(provider.GetRequiredService<AutomationsViewModel>());
    }

    private sealed class UtcClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeScheduler : IAutomationScheduler
    {
        public List<AutomationSchedule> Schedules { get; } = [];

        public List<string> RunNowRequests { get; } = [];

        public Exception? RunNowFailure { get; set; }

        public Task<IReadOnlyList<AutomationSchedule>> GetSchedulesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AutomationSchedule>>(Schedules);

        public Task RunNowAsync(string automationId, CancellationToken ct = default)
        {
            RunNowRequests.Add(automationId);
            return RunNowFailure is null ? Task.CompletedTask : Task.FromException(RunNowFailure);
        }
    }

    private sealed class FakeRunLog : IAutomationLogger
    {
        public List<AutomationRunRecord> Runs { get; } = [];

        public Exception? Failure { get; set; }

        public Task<Guid> BeginRunAsync(string automationId, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());

        public Task CompleteRunAsync(Guid runId, AutomationRunOutcome outcome, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<AutomationRunRecord>> GetRecentRunsAsync(int count, CancellationToken ct = default)
            => Failure is null
                ? Task.FromResult<IReadOnlyList<AutomationRunRecord>>(Runs)
                : Task.FromException<IReadOnlyList<AutomationRunRecord>>(Failure);
    }

    private sealed class FakeCredentials : IAutomationCredentialService
    {
        public Dictionary<AutomationSite, SiteCredentials> Logins { get; } = [];

        public SiteCredentials? Get(AutomationSite site) => Logins.GetValueOrDefault(site);

        public void Save(AutomationSite site, SiteCredentials credentials) => Logins[site] = credentials;

        public void Clear(AutomationSite site) => Logins.Remove(site);
    }

    private sealed class FakePlans : IRemoteWorkPlanRepository
    {
        public RemoteWorkPlan? Saved { get; set; }

        public Task<RemoteWorkPlan> GetAsync(int year, int month, CancellationToken ct = default)
            => Task.FromResult(Saved ?? RemoteWorkPlan.Create(year, month, []));

        public Task SaveAsync(RemoteWorkPlan plan, CancellationToken ct = default)
        {
            Saved = plan;
            return Task.CompletedTask;
        }
    }
}
