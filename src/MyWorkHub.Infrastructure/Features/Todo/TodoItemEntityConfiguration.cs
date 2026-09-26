using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyWorkHub.Infrastructure.Features.Todo;

/// <summary>
/// Maps <see cref="TodoItemEntity"/> onto the existing <c>Todos</c> table. Picked up automatically by
/// <see cref="Data.AppDbContext"/> (which applies every configuration in this assembly), so a feature's
/// tables are declared inside its own slice rather than in the shared context.
/// </summary>
internal sealed class TodoItemEntityConfiguration : IEntityTypeConfiguration<TodoItemEntity>
{
    private const string TABLE_NAME = "Todos";

    public void Configure(EntityTypeBuilder<TodoItemEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TABLE_NAME);
        builder.HasKey(t => t.Id);
    }
}
