using Microsoft.EntityFrameworkCore;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Data;

namespace MyWorkHub.Infrastructure.Features.Automations.Persistence;

/// <summary>
/// <see cref="IAutomationLogger"/> over EF Core + SQLite (<c>AutomationRuns</c> table). Uses a short-lived
/// context per call, so the logger itself can be a singleton shared by concurrently running jobs.
/// </summary>
internal sealed class AutomationLogger : IAutomationLogger
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public AutomationLogger(IDbContextFactory<AppDbContext> contextFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> BeginRunAsync(string automationId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        var run = new AutomationRunEntity
        {
            Id = Guid.NewGuid(),
            JobName = automationId,
            StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Status = AutomationRunStatus.PENDING,
        };

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Add(run);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return run.Id;
    }

    public async Task CompleteRunAsync(Guid runId, AutomationRunOutcome outcome, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var completedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var details = AutomationJson.SerializeSteps(outcome.Steps);

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await db.Set<AutomationRunEntity>()
            .Where(r => r.Id == runId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.CompletedAt, completedAt)
                    .SetProperty(r => r.Status, outcome.Status)
                    .SetProperty(r => r.ErrorMessage, outcome.ErrorMessage)
                    .SetProperty(r => r.Details, details),
                ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AutomationRunRecord>> GetRecentRunsAsync(int count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var rows = await db.Set<AutomationRunEntity>()
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Select(r => new AutomationRunRecord(
                r.Id,
                r.JobName,
                r.StartedAt,
                r.CompletedAt,
                r.Status,
                r.ErrorMessage,
                AutomationJson.DeserializeSteps(r.Details)))
            .ToList();
    }
}
