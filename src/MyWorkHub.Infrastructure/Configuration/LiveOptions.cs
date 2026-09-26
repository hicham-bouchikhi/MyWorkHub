namespace MyWorkHub.Infrastructure.Configuration;

/// <summary>
/// An options record re-read on every access instead of being captured once at registration. Registered
/// for the sections the Settings page edits (<c>AzureDevOps</c>, <c>Workspace</c>) as
/// <c>new LiveOptions&lt;T&gt;(() =&gt; T.FromConfiguration(configuration))</c>: the configuration reloads
/// when <c>appsettings.json</c> changes, so a consumer that reads <see cref="Current"/> per operation
/// picks up a saved edit without an app restart. The <c>FromConfiguration</c> factories only read a few
/// keys, so re-reading is cheap. Consumers read <see cref="Current"/> once per operation and use that
/// snapshot throughout, so one operation never mixes old and new values.
/// </summary>
internal sealed class LiveOptions<T>
    where T : class
{
    private readonly Func<T> _read;

    public LiveOptions(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        _read = read;
    }

    /// <summary>The options as currently configured.</summary>
    public T Current => _read();
}
