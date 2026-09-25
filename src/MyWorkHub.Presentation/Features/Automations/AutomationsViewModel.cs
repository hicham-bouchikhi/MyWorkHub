using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Automations;

/// <summary>
/// The Automations page: each automation with its schedule and a "Run now" button, the recent run history
/// with per-step outcomes, the logins the site automations use, and this month's remote-work plan.
/// The work itself happens in the background jobs; this page only queues runs and reads their history.
/// Every service is optional (the page shows whichever sections are available).
/// </summary>
public sealed partial class AutomationsViewModel : PageViewModel
{
    private const int HISTORY_SIZE = 30;

    private readonly IAutomationScheduler? _scheduler;
    private readonly IAutomationLogger? _runLog;
    private readonly IAutomationCredentialService? _credentials;
    private readonly IRemoteWorkPlanRepository? _plans;
    private readonly TimeProvider _timeProvider;
    private bool _isLoaded;

    public AutomationsViewModel(
        IAutomationScheduler? scheduler = null,
        IAutomationLogger? runLog = null,
        IAutomationCredentialService? credentials = null,
        IRemoteWorkPlanRepository? plans = null,
        TimeProvider? timeProvider = null)
        : base("Automations")
    {
        _scheduler = scheduler;
        _runLog = runLog;
        _credentials = credentials;
        _plans = plans;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var today = _timeProvider.GetLocalNow();
        PlanYear = today.Year;
        PlanMonth = today.Month;
        PlanMonthLabel = today.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        if (_credentials is not null)
        {
            SiteLogins.Add(LoginRow(AutomationSite.TCL, "TCL (transport pass)"));
            SiteLogins.Add(LoginRow(AutomationSite.PEOPLENET, "PeopleNet"));
            SiteLogins.Add(LoginRow(AutomationSite.MWORK, "mwork"));
        }
    }

    /// <summary>False when no scheduler is registered; the page then shows a notice instead of the automations.</summary>
    public bool IsAvailable => _scheduler is not null;

    public bool HasHistory => _runLog is not null;

    public bool CanManageLogins => _credentials is not null;

    public bool CanEditPlan => _plans is not null;

    public ObservableCollection<AutomationRowViewModel> Automations { get; } = [];

    /// <summary>Recent runs of every automation, newest first.</summary>
    public ObservableCollection<AutomationRunRowViewModel> Runs { get; } = [];

    public ObservableCollection<SiteLoginRowViewModel> SiteLogins { get; } = [];

    public int PlanYear { get; }

    public int PlanMonth { get; }

    /// <summary>The month the plan editor edits (the current one), e.g. "September 2026".</summary>
    public string PlanMonthLabel { get; }

    /// <summary>The plan's days as typed by the user: day numbers separated by commas or spaces.</summary>
    [ObservableProperty]
    private string _planDaysText = "";

    /// <summary>Result of the last plan save (saved / why it was rejected); null before any.</summary>
    [ObservableProperty]
    private string? _planMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Loads on first display; later visits keep what is shown (use <see cref="RefreshCommand"/>).</summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        if (_isLoaded)
        {
            return;
        }

        await RefreshAsync(ct);
        await LoadPlanAsync(ct);
        _isLoaded = true;
    }

    /// <summary>Re-reads schedules and run history (e.g. after a queued run finished).</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            var zone = _timeProvider.LocalTimeZone;
            if (_scheduler is not null)
            {
                var schedules = await _scheduler.GetSchedulesAsync(ct);
                Automations.Clear();
                foreach (var schedule in schedules)
                {
                    Automations.Add(new AutomationRowViewModel(schedule, zone));
                }
            }

            if (_runLog is not null)
            {
                var runs = await _runLog.GetRecentRunsAsync(HISTORY_SIZE, ct);
                Runs.Clear();
                foreach (var run in runs)
                {
                    Runs.Add(new AutomationRunRowViewModel(run, zone));
                }
            }

            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not load the automations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Queues an immediate run; its outcome shows up in the history (and as a notification) when done.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RunNowAsync(AutomationRowViewModel row, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_scheduler is null)
        {
            return;
        }

        try
        {
            await _scheduler.RunNowAsync(row.Id, ct);
            row.RunNowStatus = "Started — you will be notified when it finishes.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.RunNowStatus = $"Could not start: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SaveLogin(SiteLoginRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_credentials is null || !row.CanSave)
        {
            return;
        }

        try
        {
            _credentials.Save(row.Site, new SiteCredentials(row.UserName.Trim(), row.Password));
            row.HasSavedLogin = true;
            row.UserName = "";
            row.Password = "";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not save the {row.Name} login: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearLogin(SiteLoginRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (_credentials is null)
        {
            return;
        }

        try
        {
            _credentials.Clear(row.Site);
            row.HasSavedLogin = false;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not remove the {row.Name} login: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SavePlanAsync(CancellationToken ct)
    {
        if (_plans is null)
        {
            return;
        }

        if (!RemoteWorkPlan.TryParse(PlanYear, PlanMonth, PlanDaysText, out var plan, out var error))
        {
            PlanMessage = error;
            return;
        }

        try
        {
            await _plans.SaveAsync(plan!, ct);
            PlanDaysText = plan!.FormatDays();
            PlanMessage = plan.Days.Count == 0
                ? "Saved: no remote-work days this month."
                : $"Saved: {plan.Days.Count} remote-work day(s).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlanMessage = $"Could not save the plan: {ex.Message}";
        }
    }

    private async Task LoadPlanAsync(CancellationToken ct)
    {
        if (_plans is null)
        {
            return;
        }

        try
        {
            PlanDaysText = (await _plans.GetAsync(PlanYear, PlanMonth, ct)).FormatDays();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PlanMessage = $"Could not load the plan: {ex.Message}";
        }
    }

    private SiteLoginRowViewModel LoginRow(AutomationSite site, string name)
        => new(site, name, _credentials!.Get(site) is not null);
}
