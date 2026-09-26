using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// Maps <see cref="SeenWorkItemMentionEntity"/> onto the existing <c>SeenMentions</c> table. Picked up
/// automatically by <see cref="Data.AppDbContext"/> (which applies every configuration in this assembly).
/// </summary>
internal sealed class SeenWorkItemMentionEntityConfiguration : IEntityTypeConfiguration<SeenWorkItemMentionEntity>
{
    private const string TABLE_NAME = "SeenMentions";

    public void Configure(EntityTypeBuilder<SeenWorkItemMentionEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(TABLE_NAME);
        builder.HasKey(m => m.CommentId);

        // The key is Azure DevOps' comment id, never generated locally.
        builder.Property(m => m.CommentId).ValueGeneratedNever();
    }
}
