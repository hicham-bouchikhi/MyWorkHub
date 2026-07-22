using System.Text;
using Microsoft.AspNetCore.DataProtection;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Entities;
using MyWorkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MyWorkHub.Infrastructure.Security;

/// <summary>
/// <see cref="ICredentialStore"/> backed by ASP.NET Core Data Protection. Secrets are
/// encrypted with a per-user key ring, base64-encoded, and persisted in the
/// <see cref="AppCredential"/> table. The underlying protection is cross-platform:
/// DPAPI on Windows, AES-256-CBC + HMAC-SHA256 (key ring on disk) on Linux/macOS.
/// </summary>
public sealed class DataProtectionCredentialStore : ICredentialStore
{
    private const string PROTECTOR_PURPOSE = "MyWorkHub.CredentialStore.v1";

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IDataProtector _protector;

    public DataProtectionCredentialStore(
        IDbContextFactory<AppDbContext> contextFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _contextFactory = contextFactory;
        _protector = dataProtectionProvider.CreateProtector(PROTECTOR_PURPOSE);
    }

    public void Save(string key, string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        var encrypted = Convert.ToBase64String(
            _protector.Protect(Encoding.UTF8.GetBytes(plaintext)));

        using var db = _contextFactory.CreateDbContext();
        var existing = db.Credentials.Find(key);
        if (existing is null)
        {
            db.Credentials.Add(new AppCredential
            {
                Key = key,
                EncryptedValue = encrypted,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.EncryptedValue = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        db.SaveChanges();
    }

    public string? Get(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var db = _contextFactory.CreateDbContext();
        var existing = db.Credentials.Find(key);
        if (existing is null)
        {
            return null;
        }

        var cipher = Convert.FromBase64String(existing.EncryptedValue);
        var plaintext = _protector.Unprotect(cipher);
        return Encoding.UTF8.GetString(plaintext);
    }

    public void Delete(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var db = _contextFactory.CreateDbContext();
        var existing = db.Credentials.Find(key);
        if (existing is not null)
        {
            db.Credentials.Remove(existing);
            db.SaveChanges();
        }
    }
}
