using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>Plain-dictionary <see cref="ICredentialStore"/> (no encryption, no database).</summary>
internal sealed class InMemoryCredentialStore : ICredentialStore
{
    public Dictionary<string, string> Values { get; } = [];

    public void Save(string key, string plaintext) => Values[key] = plaintext;

    public string? Get(string key) => Values.GetValueOrDefault(key);

    public void Delete(string key) => Values.Remove(key);
}
