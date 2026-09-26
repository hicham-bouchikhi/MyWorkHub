using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Presentation.Features.Automations;

/// <summary>Formatting shared by the Automations page rows: local wall-clock times, invariant layout.</summary>
internal static class AutomationDisplay
{
    private const string TIME_FORMAT = "yyyy-MM-dd HH:mm";

    public static string LocalTime(DateTimeOffset instant, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(instant, zone).ToString(TIME_FORMAT, CultureInfo.InvariantCulture);

    public static string LocalTime(DateTime utc, TimeZoneInfo zone)
        => LocalTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), zone);
}

/// <summary>One automation card: what it does, when it next runs, and its "Run now" state.</summary>
public sealed partial class AutomationRowViewModel : ObservableObject
{
    public AutomationRowViewModel(AutomationSchedule schedule, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(zone);

        Id = schedule.Automation.Id;
        Name = schedule.Automation.DisplayName;
        Description = schedule.Automation.Description;
        ScheduleText = schedule.CronExpression is { } cron
            ? $"Scheduled (cron: {cron})"
            : schedule.Note ?? "Not scheduled.";
        NextRunText = schedule.NextRunAt is { } next
            ? $"Next run: {AutomationDisplay.LocalTime(next, zone)}"
            : "";
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    /// <summary>The cron expression it runs on, or why it only runs on demand.</summary>
    public string ScheduleText { get; }

    /// <summary>"Next run: …", empty when not scheduled.</summary>
    public string NextRunText { get; }

    /// <summary>Feedback for the last "Run now" (queued / could not start); null before any.</summary>
    [ObservableProperty]
    private string? _runNowStatus;
}

/// <summary>One step of a recorded run.</summary>
/// <param name="Name">Step name.</param>
/// <param name="Glyph">Status icon.</param>
/// <param name="Status">The step's status.</param>
/// <param name="Message">Error, skip reason or success note; empty when none.</param>
public sealed record AutomationStepRowViewModel(string Name, string Glyph, AutomationStepStatus Status, string Message)
{
    public static AutomationStepRowViewModel From(AutomationStepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var glyph = step.Status switch
        {
            AutomationStepStatus.SUCCESS => "✔",
            AutomationStepStatus.FAILED => "✖",
            _ => "–",
        };
        return new AutomationStepRowViewModel(step.Name, glyph, step.Status, step.Message ?? "");
    }

    public bool IsFailed => Status == AutomationStepStatus.FAILED;
}

/// <summary>One recorded run in the history list, with its per-step outcomes.</summary>
public sealed class AutomationRunRowViewModel
{
    public AutomationRunRowViewModel(AutomationRunRecord run, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(zone);

        Name = AutomationCatalog.DisplayNameOf(run.AutomationId);
        Status = run.Status;
        StartedText = AutomationDisplay.LocalTime(run.StartedAt, zone);
        StatusText = run.Status switch
        {
            AutomationRunStatus.PENDING => "Running",
            AutomationRunStatus.SUCCESS => "Succeeded",
            AutomationRunStatus.PARTIAL => "Partly failed",
            AutomationRunStatus.FAILED => "Failed",
            AutomationRunStatus.CANCELLED => "Cancelled",
            _ => run.Status.ToString(),
        };
        ErrorMessage = run.ErrorMessage ?? "";
        Steps = [.. run.Steps.Select(AutomationStepRowViewModel.From)];
    }

    public string Name { get; }

    public AutomationRunStatus Status { get; }

    public string StartedText { get; }

    public string StatusText { get; }

    public string ErrorMessage { get; }

    public IReadOnlyList<AutomationStepRowViewModel> Steps { get; }

    public bool IsSuccess => Status == AutomationRunStatus.SUCCESS;

    public bool IsPartial => Status == AutomationRunStatus.PARTIAL;

    public bool IsFailed => Status == AutomationRunStatus.FAILED;
}

/// <summary>The login form of one external site. The password is cleared as soon as it is saved.</summary>
public sealed partial class SiteLoginRowViewModel : ObservableObject
{
    public SiteLoginRowViewModel(AutomationSite site, string name, bool hasSavedLogin)
    {
        Site = site;
        Name = name;
        _hasSavedLogin = hasSavedLogin;
    }

    public AutomationSite Site { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _hasSavedLogin;

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _password = "";

    public bool CanSave => !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);

    partial void OnUserNameChanged(string value) => OnPropertyChanged(nameof(CanSave));

    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(CanSave));
}
