using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations.Persistence;

/// <summary>
/// EF Core row of the <c>AutomationRuns</c> table. Persistence-only: the rest of the app sees the immutable
/// <see cref="AutomationRunRecord"/>. The column set is exactly the one created by the <c>InitialCreate</c>
/// migration, so existing databases keep working without a new migration.
/// </summary>
internal sealed class AutomationRunEntity
{
    public Guid Id { get; set; }

    /// <summary>The automation id (column name kept from the original schema).</summary>
    public string JobName { get; set; } = "";

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public AutomationRunStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>JSON array of per-step results (see <see cref="AutomationJson"/>).</summary>
    public string? Details { get; set; }
}

/// <summary>
/// Maps <see cref="AutomationRunEntity"/> onto the existing <c>AutomationRuns</c> table. Picked up
/// automatically by <see cref="Data.AppDbContext"/> (which applies every configuration in this assembly).
/// </summary>
internal sealed class AutomationRunEntityConfiguration : IEntityTypeConfiguration<AutomationRunEntity>
{
    private const string TABLE_NAME = "AutomationRuns";

    public void Configure(EntityTypeBuilder<AutomationRunEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TABLE_NAME);
        builder.HasKey(r => r.Id);

        // Stored by name, as the original schema did, so the history stays readable in the raw database.
        builder.Property(r => r.Status).HasConversion<string>();
    }
}
