using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Data;

/// <summary>
/// <see cref="IAutomationLogger"/> over EF Core + SQLite (T049). Records each automation job
/// execution as an <see cref="AutomationRun"/> and exposes its run history. Uses a short-lived
/// context per call via <see cref="IDbContextFactory{TContext}"/>, matching <see cref="TodoRepository"/>.
/// </summary>
public sealed class AutomationLogger : IAutomationLogger
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public AutomationLogger(IDbContextFactory<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<Guid> BeginRunAsync(string jobName, CancellationToken ct = default)
    {
        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            StartedAt = DateTime.UtcNow,
            Status = AutomationStatus.PENDING,
        };

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.AutomationRuns.Add(run);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return run.Id;
    }

    public async Task CompleteRunAsync(Guid runId, bool success, string? errorMessage = null, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var run = await db.AutomationRuns.FindAsync([runId], ct).ConfigureAwait(false);
        if (run is null)
        {
            return;
        }

        run.CompletedAt = DateTime.UtcNow;
        run.Status = success ? AutomationStatus.SUCCESS : AutomationStatus.FAILED;
        run.ErrorMessage = errorMessage;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AutomationRun>> GetHistoryAsync(string jobName, int count = 30, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.AutomationRuns
            .AsNoTracking()
            .Where(r => r.JobName == jobName)
            .OrderByDescending(r => r.StartedAt)
            .Take(count)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
