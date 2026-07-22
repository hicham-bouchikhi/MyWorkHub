using Microsoft.AspNetCore.DataProtection;
using MyWorkHub.Infrastructure.Data;
using MyWorkHub.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="DataProtectionCredentialStore"/> against a real
/// SQLite file and a real (ephemeral) Data Protection provider.
/// </summary>
public sealed class DataProtectionCredentialStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestContextFactory _factory;
    private readonly IDataProtectionProvider _dataProtection = new EphemeralDataProtectionProvider();

    public DataProtectionCredentialStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cwh-test-{Guid.NewGuid():N}.db");
        _factory = new TestContextFactory(_dbPath);
        using var db = _factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    private DataProtectionCredentialStore CreateStore() => new(_factory, _dataProtection);

    [Fact]
    public void Save_then_Get_returns_the_original_plaintext()
    {
        var store = CreateStore();

        store.Save("TCL_PASSWORD", "s3cr3t!");

        Assert.Equal("s3cr3t!", store.Get("TCL_PASSWORD"));
    }

    [Fact]
    public void Get_returns_null_for_an_unknown_key()
    {
        var store = CreateStore();

        Assert.Null(store.Get("DOES_NOT_EXIST"));
    }

    [Fact]
    public void Save_twice_updates_the_stored_value()
    {
        var store = CreateStore();

        store.Save("MWORK_PASS", "old-password");
        store.Save("MWORK_PASS", "new-password");

        Assert.Equal("new-password", store.Get("MWORK_PASS"));
    }

    [Fact]
    public void Delete_removes_the_stored_value()
    {
        var store = CreateStore();
        store.Save("PEOPLENET_PASS", "x");

        store.Delete("PEOPLENET_PASS");

        Assert.Null(store.Get("PEOPLENET_PASS"));
    }

    [Fact]
    public void Stored_value_is_never_persisted_in_plaintext()
    {
        var store = CreateStore();

        store.Save("API_TOKEN", "supersecret");

        using var db = _factory.CreateDbContext();
        var row = db.Credentials.Single();
        Assert.DoesNotContain("supersecret", row.EncryptedValue, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private sealed class TestContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _path;

        public TestContextFactory(string path) => _path = path;

        public AppDbContext CreateDbContext()
            // Pooling=False so SQLite releases the file handle when the context is
            // disposed, allowing the temp db to be deleted in test teardown.
            => new(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_path};Pooling=False")
                .Options);
    }
}
