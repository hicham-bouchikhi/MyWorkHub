using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyWorkHub.Infrastructure.Features.Automations.Persistence;

/// <summary>
/// EF Core row of the <c>RemoteWorkSchedules</c> table: one month's remote-work plan. The column set is
/// exactly the one created by the <c>InitialCreate</c> migration, so existing databases keep working
/// without a new migration.
/// </summary>
internal sealed class RemoteWorkScheduleEntity
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>JSON array of day-of-month numbers, e.g. <c>[2,5,9]</c>.</summary>
    public string DaysJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    // Legacy columns from the pre-rewrite schema. Per-step sync outcomes now live in the run history
    // (AutomationRuns), so these are not written, but they stay mapped so no destructive migration is needed.
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncStatusJson { get; set; }
}

/// <summary>Maps <see cref="RemoteWorkScheduleEntity"/> onto the existing <c>RemoteWorkSchedules</c> table.</summary>
internal sealed class RemoteWorkScheduleEntityConfiguration : IEntityTypeConfiguration<RemoteWorkScheduleEntity>
{
    private const string TABLE_NAME = "RemoteWorkSchedules";

    public void Configure(EntityTypeBuilder<RemoteWorkScheduleEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TABLE_NAME);
        builder.HasKey(s => s.Id);
    }
}
