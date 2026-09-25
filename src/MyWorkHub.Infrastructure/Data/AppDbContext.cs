using MyWorkHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<AppCredential> Credentials => Set<AppCredential>();
    public DbSet<RemoteWorkSchedule> RemoteWorkSchedules => Set<RemoteWorkSchedule>();
    public DbSet<SeenWorkItemMention> SeenMentions => Set<SeenWorkItemMention>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // Feature slices declare their own tables (e.g. Features/Todo/TodoItemEntityConfiguration),
        // so adding a feature's entity never means editing this shared context.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // AppCredential uses the string Key as its primary key (no generated Id).
        modelBuilder.Entity<AppCredential>().HasKey(c => c.Key);

        // Persist the run status as a readable string rather than an int.
        modelBuilder.Entity<AutomationRun>()
            .Property(r => r.Status)
            .HasConversion<string>();

        modelBuilder.Entity<SeenWorkItemMention>()
            .HasKey(m => m.CommentId);
        modelBuilder.Entity<SeenWorkItemMention>()
            .Property(m => m.CommentId)
            .ValueGeneratedNever();
    }
}
