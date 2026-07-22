using MyWorkHub.Core.Entities;
using MyWorkHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Tests.Data;

public sealed class AutomationLoggerTests
{
    [Fact]
    public async Task BeginRunAsync_creates_pending_run()
    {
        using var factory = new InMemorySqliteContextFactory();
        var logger = new AutomationLogger(factory);
        var ct = TestContext.Current.CancellationToken;

        await logger.BeginRunAsync("RemoteWorkSync", ct);

        var history = await logger.GetHistoryAsync("RemoteWorkSync", 30, ct);
        var run = Assert.Single(history);
        Assert.Equal(AutomationStatus.PENDING, run.Status);
        Assert.Equal("RemoteWorkSync", run.JobName);
        Assert.NotEqual(default, run.StartedAt);
    }

    [Fact]
    public async Task CompleteRunAsync_success_sets_status_and_completedAt()
    {
        using var factory = new InMemorySqliteContextFactory();
        var logger = new AutomationLogger(factory);
        var ct = TestContext.Current.CancellationToken;
        var runId = await logger.BeginRunAsync("RemoteWorkSync", ct);

        await logger.CompleteRunAsync(runId, success: true, errorMessage: null, ct);

        var run = Assert.Single(await logger.GetHistoryAsync("RemoteWorkSync", 30, ct));
        Assert.Equal(AutomationStatus.SUCCESS, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task CompleteRunAsync_failure_sets_status_and_errorMessage()
    {
        using var factory = new InMemorySqliteContextFactory();
        var logger = new AutomationLogger(factory);
        var ct = TestContext.Current.CancellationToken;
        var runId = await logger.BeginRunAsync("RemoteWorkSync", ct);

        await logger.CompleteRunAsync(runId, success: false, errorMessage: "boom", ct);

        var run = Assert.Single(await logger.GetHistoryAsync("RemoteWorkSync", 30, ct));
        Assert.Equal(AutomationStatus.FAILED, run.Status);
        Assert.Equal("boom", run.ErrorMessage);
    }

    [Fact]
    public async Task GetHistoryAsync_returns_newest_first()
    {
        using var factory = new InMemorySqliteContextFactory();
        var logger = new AutomationLogger(factory);
        var ct = TestContext.Current.CancellationToken;

        var firstId = await logger.BeginRunAsync("RemoteWorkSync", ct);
        await SetStartedAt(factory, firstId, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), ct);
        var secondId = await logger.BeginRunAsync("RemoteWorkSync", ct);
        await SetStartedAt(factory, secondId, new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), ct);

        var history = await logger.GetHistoryAsync("RemoteWorkSync", 30, ct);

        Assert.Equal(secondId, history[0].Id);
    }

    [Fact]
    public async Task GetHistoryAsync_respects_count_limit()
    {
        using var factory = new InMemorySqliteContextFactory();
        var logger = new AutomationLogger(factory);
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < 5; i++)
        {
            await logger.BeginRunAsync("RemoteWorkSync", ct);
        }

        var history = await logger.GetHistoryAsync("RemoteWorkSync", 3, ct);

        Assert.Equal(3, history.Count);
    }

    private static async Task SetStartedAt(
        IDbContextFactory<AppDbContext> factory, Guid runId, DateTime startedAt, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.AutomationRuns.FindAsync([runId], ct);
        run!.StartedAt = startedAt;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// An <see cref="IDbContextFactory{TContext}"/> over a single in-memory SQLite connection
    /// (kept open for the test's lifetime so every context shares the same database).
    /// </summary>
    private sealed class InMemorySqliteContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public InMemorySqliteContextFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var context = new AppDbContext(_options);
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateDbContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
