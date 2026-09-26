using Microsoft.EntityFrameworkCore;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Data;

namespace MyWorkHub.Infrastructure.Features.Automations.Persistence;

/// <summary>
/// <see cref="IRemoteWorkPlanRepository"/> over EF Core + SQLite (<c>RemoteWorkSchedules</c> table, one row
/// per month). Uses a short-lived context per call, so the repository itself can be a singleton.
/// </summary>
internal sealed class RemoteWorkPlanRepository : IRemoteWorkPlanRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;

    public RemoteWorkPlanRepository(IDbContextFactory<AppDbContext> contextFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
    }

    public async Task<RemoteWorkPlan> GetAsync(int year, int month, CancellationToken ct = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await FindMonthAsync(db, year, month, ct).ConfigureAwait(false);

        // Days outside the month (a hand-edited row) are dropped rather than failing the whole plan.
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var days = AutomationJson.DeserializeDays(row?.DaysJson).Where(d => d >= 1 && d <= daysInMonth);
        return RemoteWorkPlan.Create(year, month, days);
    }

    public async Task SaveAsync(RemoteWorkPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var daysJson = AutomationJson.SerializeDays(plan.Days.Select(d => d.Day));

        await using var db = await _contextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var row = await FindMonthAsync(db, plan.Year, plan.Month, ct).ConfigureAwait(false);
        if (row is null)
        {
            db.Add(new RemoteWorkScheduleEntity
            {
                Id = Guid.NewGuid(),
                Year = plan.Year,
                Month = plan.Month,
                DaysJson = daysJson,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            });
        }
        else
        {
            row.DaysJson = daysJson;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // The table has no unique (Year, Month) index; should duplicates exist, the newest row wins.
    private static Task<RemoteWorkScheduleEntity?> FindMonthAsync(AppDbContext db, int year, int month, CancellationToken ct)
        => db.Set<RemoteWorkScheduleEntity>()
            .Where(s => s.Year == year && s.Month == month)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
