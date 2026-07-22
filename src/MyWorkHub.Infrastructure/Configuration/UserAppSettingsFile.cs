using System.Text.Json;
using System.Text.Json.Nodes;
using MyWorkHub.Core;

namespace MyWorkHub.Infrastructure.Configuration;

/// <summary>
/// Read/modify/write helper for the user's <c>appsettings.json</c>
/// (<see cref="AppPaths.UserAppSettingsPath"/>). Uses the mutable <see cref="JsonNode"/>
/// DOM so untouched sections are preserved verbatim, and so it stays free of the
/// reflection-based serializer that the IL2026/IL3050 tripwire forbids.
/// </summary>
public static class UserAppSettingsFile
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    // Tolerate // comments and trailing content so a hand-edited (or previously seeded,
    // comment-bearing) file doesn't crash the read/modify/write cycle.
    private static readonly JsonDocumentOptions _documentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Persists the Azure DevOps organization URL and project list, leaving the rest of the file intact.</summary>
    public static void SaveAzureDevOps(string organizationUrl, IReadOnlyList<string> projects)
    {
        ArgumentNullException.ThrowIfNull(organizationUrl);
        ArgumentNullException.ThrowIfNull(projects);

        var root = Load();

        var azdo = root["AzureDevOps"] as JsonObject ?? [];
        azdo["OrganizationUrl"] = organizationUrl;

        var array = new JsonArray();
        foreach (var project in projects)
        {
            array.Add(project);
        }

        azdo["Projects"] = array;
        root["AzureDevOps"] = azdo;

        File.WriteAllText(AppPaths.UserAppSettingsPath, root.ToJsonString(_writeOptions));
    }

    /// <summary>Persists the code-review workspace settings, leaving the rest of the file intact.</summary>
    public static void SaveWorkspace(string workFolderPath, string claudeExecutablePath, string reviewModelId)
    {
        ArgumentNullException.ThrowIfNull(workFolderPath);
        ArgumentNullException.ThrowIfNull(claudeExecutablePath);
        ArgumentNullException.ThrowIfNull(reviewModelId);

        var root = Load();

        var workspace = root["Workspace"] as JsonObject ?? [];
        workspace["WorkFolderPath"] = workFolderPath;
        workspace["ClaudeExecutablePath"] = claudeExecutablePath;
        workspace["ReviewModelId"] = reviewModelId;
        root["Workspace"] = workspace;

        File.WriteAllText(AppPaths.UserAppSettingsPath, root.ToJsonString(_writeOptions));
    }

    /// <summary>Persists the watched email folder ids, leaving the rest of the file intact.</summary>
    public static void SaveEmailFolders(IReadOnlyList<string> folderIds)
    {
        ArgumentNullException.ThrowIfNull(folderIds);

        var root = Load();

        var email = root["Email"] as JsonObject ?? [];

        var array = new JsonArray();
        foreach (var id in folderIds)
        {
            array.Add(id);
        }

        email["FolderIds"] = array;
        root["Email"] = email;

        File.WriteAllText(AppPaths.UserAppSettingsPath, root.ToJsonString(_writeOptions));
    }

    private static JsonObject Load()
    {
        var path = AppPaths.UserAppSettingsPath;
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);
        return JsonNode.Parse(text, documentOptions: _documentOptions) as JsonObject ?? [];
    }
}
