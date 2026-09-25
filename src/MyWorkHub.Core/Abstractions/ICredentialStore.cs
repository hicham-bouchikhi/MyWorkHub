namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Stores secrets encrypted at rest (ASP.NET Core Data Protection: DPAPI on Windows, a local key
/// ring elsewhere) — never in <c>appsettings.json</c>. Values are referenced by key
/// from <see cref="CredentialKeys"/> — never a string literal at the call site.
/// </summary>
public interface ICredentialStore
{
    void Save(string key, string plaintext);
    string? Get(string key);
    void Delete(string key);
}
