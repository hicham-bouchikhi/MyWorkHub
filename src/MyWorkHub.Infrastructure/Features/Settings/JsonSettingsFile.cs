using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyWorkHub.Infrastructure.Features.Settings;

/// <summary>
/// Read-modify-write access to the user's <c>appsettings.json</c> that touches only the keys a caller sets.
/// Every update re-reads the file (so edits made by hand since the last save are kept), applies the change to
/// the JSON tree, and replaces the file <b>atomically</b>: the new content is written and flushed to a temp
/// file in the same directory, then moved over the original, so a crash mid-write leaves either the old or
/// the new file — never a truncated one. A file that is not valid JSON is never overwritten (the parse error
/// propagates). Comments in a hand-edited file are not preserved; values, key order and every other section are.
/// </summary>
internal sealed class JsonSettingsFile : IDisposable
{
    private static readonly JsonDocumentOptions _readOptions = new()
    {
        // Same leniency as the configuration's JSON provider, so any file the app can load can be edited.
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        // A local, human-edited file: keep accents, '+', '<' etc. readable instead of \uXXXX-escaping them.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="path">The settings file; it (and its directory) is created on first save if missing.</param>
    public JsonSettingsFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    /// <summary>Applies <paramref name="update"/> to the file's root object and saves the result atomically.</summary>
    /// <exception cref="JsonException">The existing file is not valid JSON (it is left untouched).</exception>
    /// <exception cref="InvalidDataException">The existing file's root is not a JSON object.</exception>
    public async Task UpdateAsync(Action<JsonObject> update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        // Serialize writers within the app: two quick saves must not interleave read-modify-write cycles.
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var root = await ReadRootAsync(ct).ConfigureAwait(false);
            update(root);
            ReplaceAtomically(root.ToJsonString(_writeOptions) + Environment.NewLine);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The child object <paramref name="name"/> of <paramref name="parent"/>, created when missing (or when
    /// the existing value is not an object). Matched case-insensitively, like configuration keys, so a
    /// hand-written <c>"workspace"</c> is updated rather than duplicated.
    /// </summary>
    public static JsonObject Section(JsonObject parent, string name)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var key = ExistingKey(parent, name) ?? name;
        if (parent[key] is JsonObject section)
        {
            return section;
        }

        section = [];
        parent[key] = section;
        return section;
    }

    /// <summary>Sets <paramref name="name"/> on <paramref name="section"/>, reusing an existing key's casing.</summary>
    public static void Set(JsonObject section, string name, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(section);
        section[ExistingKey(section, name) ?? name] = value;
    }

    public void Dispose() => _gate.Dispose();

    private static string? ExistingKey(JsonObject obj, string name)
        => obj.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    private async Task<JsonObject> ReadRootAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            return [];
        }

        var text = await File.ReadAllTextAsync(Path, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return JsonNode.Parse(text, documentOptions: _readOptions) as JsonObject
            ?? throw new InvalidDataException($"{Path} does not contain a JSON object; it was left unchanged.");
    }

    // Synchronous on purpose: the file is a few KB, and flushing through to the disk has no async API.
    private void ReplaceAtomically(string content)
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);

        // Same directory, so the final move is a rename on the same volume (atomic), and a name the
        // configuration's file watcher ignores (it only watches appsettings.json itself).
        var temp = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(Path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));

                // On disk before the rename, so a power loss cannot leave a renamed-but-empty file.
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, Path, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }
}
