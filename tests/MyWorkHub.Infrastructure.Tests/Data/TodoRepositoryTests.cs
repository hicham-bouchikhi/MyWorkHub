using MyWorkHub.Core.Entities;
using MyWorkHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Tests.Data;

public sealed class TodoRepositoryTests
{
    [Fact]
    public async Task Added_item_is_returned_by_GetAll()
    {
        using var factory = new InMemorySqliteContextFactory();
        var repo = new TodoRepository(factory);
        var item = new TodoItem { Id = Guid.NewGuid(), Title = "Buy milk", CreatedAt = DateTime.UtcNow };
        var ct = TestContext.Current.CancellationToken;

        await repo.AddAsync(item, ct);
        var all = await repo.GetAllAsync(ct);

        Assert.Single(all);
        Assert.Equal("Buy milk", all[0].Title);
    }

    [Fact]
    public async Task Update_persists_completion()
    {
        using var factory = new InMemorySqliteContextFactory();
        var repo = new TodoRepository(factory);
        var ct = TestContext.Current.CancellationToken;
        var item = new TodoItem { Id = Guid.NewGuid(), Title = "Ship it", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(item, ct);

        item.IsCompleted = true;
        item.CompletedAt = DateTime.UtcNow;
        await repo.UpdateAsync(item, ct);

        var reloaded = (await repo.GetAllAsync(ct)).Single();
        Assert.True(reloaded.IsCompleted);
        Assert.NotNull(reloaded.CompletedAt);
    }

    [Fact]
    public async Task Delete_removes_the_item()
    {
        using var factory = new InMemorySqliteContextFactory();
        var repo = new TodoRepository(factory);
        var ct = TestContext.Current.CancellationToken;
        var item = new TodoItem { Id = Guid.NewGuid(), Title = "Obsolete", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(item, ct);

        await repo.DeleteAsync(item.Id, ct);

        Assert.Empty(await repo.GetAllAsync(ct));
    }

    [Fact]
    public async Task Delete_is_a_no_op_for_an_unknown_id()
    {
        using var factory = new InMemorySqliteContextFactory();
        var repo = new TodoRepository(factory);
        var ct = TestContext.Current.CancellationToken;
        await repo.AddAsync(new TodoItem { Id = Guid.NewGuid(), Title = "Keep", CreatedAt = DateTime.UtcNow }, ct);

        await repo.DeleteAsync(Guid.NewGuid(), ct);

        Assert.Single(await repo.GetAllAsync(ct));
    }

    [Fact]
    public async Task GetAll_returns_newest_first()
    {
        using var factory = new InMemorySqliteContextFactory();
        var repo = new TodoRepository(factory);
        var ct = TestContext.Current.CancellationToken;
        await repo.AddAsync(new TodoItem { Id = Guid.NewGuid(), Title = "Older", CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc) }, ct);
        await repo.AddAsync(new TodoItem { Id = Guid.NewGuid(), Title = "Newer", CreatedAt = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc) }, ct);

        var all = await repo.GetAllAsync(ct);

        Assert.Equal("Newer", all[0].Title);
        Assert.Equal("Older", all[1].Title);
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
