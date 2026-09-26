using MyWorkHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> over one in-memory SQLite connection, kept open for the
/// factory's lifetime so every context shares the same database. The schema is built by the real
/// migrations (as at app startup), not <c>EnsureCreated</c>, so repository tests also prove the
/// feature's mapping matches the migrated tables.
/// </summary>
internal sealed class InMemorySqliteContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public InMemorySqliteContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using var context = new AppDbContext(_options);
        context.Database.Migrate();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
