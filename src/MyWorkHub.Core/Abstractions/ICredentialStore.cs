namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Stores secrets encrypted at rest (Windows DPAPI). Values are referenced by key
/// (e.g. "TCL_PASSWORD", "PEOPLENET_PASSWORD").
/// </summary>
public interface ICredentialStore
{
    void Save(string key, string plaintext);
    string? Get(string key);
    void Delete(string key);
}
