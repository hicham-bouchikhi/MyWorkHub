using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Core.Navigation;

namespace MyWorkHub.Automation.Tests;

/// <summary>In-memory <see cref="IAutomationLogger"/> recording every begin/complete call.</summary>
internal sealed class FakeAutomationLogger : IAutomationLogger
{
    public List<string> Begun { get; } = [];

    public Dictionary<Guid, AutomationRunOutcome> Completed { get; } = [];

    /// <summary>The outcome of the single completed run.</summary>
    public AutomationRunOutcome LastOutcome => Assert.Single(Completed).Value;

    public Task<Guid> BeginRunAsync(string automationId, CancellationToken ct = default)
    {
        Begun.Add(automationId);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task CompleteRunAsync(Guid runId, AutomationRunOutcome outcome, CancellationToken ct = default)
    {
        Completed[runId] = outcome;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AutomationRunRecord>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AutomationRunRecord>>([]);
}

/// <summary>A plan repository returning a fixed plan, or throwing when <see cref="Failure"/> is set.</summary>
internal sealed class FakePlanRepository : IRemoteWorkPlanRepository
{
    public RemoteWorkPlan Plan { get; set; } = RemoteWorkPlan.Create(2026, 9, [2, 9]);

    public Exception? Failure { get; set; }

    public List<(int Year, int Month)> Requests { get; } = [];

    public Task<RemoteWorkPlan> GetAsync(int year, int month, CancellationToken ct = default)
    {
        Requests.Add((year, month));
        return Failure is null ? Task.FromResult(Plan) : Task.FromException<RemoteWorkPlan>(Failure);
    }

    public Task SaveAsync(RemoteWorkPlan plan, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Records which of the three sync targets were called and throws the configured exception for a target,
/// so a test can make any one (or several) fail and check the others still ran.
/// </summary>
internal sealed class FakeRemoteWorkSync : IRemoteWorkSyncAutomation
{
    public const string OUTLOOK = "Outlook";
    public const string PEOPLENET = "PeopleNet";
    public const string MWORK = "mwork";

    public Dictionary<string, Exception> Failures { get; } = [];

    /// <summary>Invoked when a target is called, before it succeeds or throws (e.g. to cancel the run).</summary>
    public Action<string>? OnCalled { get; set; }

    public List<string> Called { get; } = [];

    public Task SyncOutlookAsync(RemoteWorkPlan plan, CancellationToken ct = default) => Run(OUTLOOK);

    public Task SyncPeopleNetAsync(RemoteWorkPlan plan, CancellationToken ct = default) => Run(PEOPLENET);

    public Task SyncMWorkAsync(RemoteWorkPlan plan, CancellationToken ct = default) => Run(MWORK);

    private Task Run(string target)
    {
        Called.Add(target);
        OnCalled?.Invoke(target);
        return Failures.TryGetValue(target, out var failure) ? Task.FromException(failure) : Task.CompletedTask;
    }
}

internal sealed class FakeTransportAutomation : ITransportReimbursementAutomation
{
    public TransportAttestation Attestation { get; } =
        new("/tmp/attestation.pdf", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

    public Exception? DownloadFailure { get; set; }

    public Exception? SendFailure { get; set; }

    public List<TransportAttestation> Sent { get; } = [];

    public Task<TransportAttestation> DownloadAttestationAsync(CancellationToken ct = default)
        => DownloadFailure is null ? Task.FromResult(Attestation) : Task.FromException<TransportAttestation>(DownloadFailure);

    public Task SendAttestationAsync(TransportAttestation attestation, CancellationToken ct = default)
    {
        Sent.Add(attestation);
        return SendFailure is null ? Task.CompletedTask : Task.FromException(SendFailure);
    }
}

internal sealed class RecordingNotificationService : INotificationService
{
    public List<(string Title, string Message, NotificationSeverity Severity)> Notifications { get; } = [];

    public void Notify(string title, string message, NotificationSeverity severity = NotificationSeverity.INFORMATION, NavigationTarget? target = null)
        => Notifications.Add((title, message, severity));
}

/// <summary>A clock frozen at a given instant, in UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
