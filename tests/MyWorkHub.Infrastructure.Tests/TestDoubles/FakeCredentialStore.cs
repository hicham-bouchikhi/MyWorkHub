using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>In-memory <see cref="ICredentialStore"/> for tests (no DPAPI, no SQLite).</summary>
internal sealed class FakeCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public void Save(string key, string plaintext) => _values[key] = plaintext;

    public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

    public void Delete(string key) => _values.Remove(key);
}
