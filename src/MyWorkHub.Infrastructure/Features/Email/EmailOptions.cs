using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Features.Settings;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary>
/// Which mail folders the Email page watches (configuration section <c>Email</c>). Folder ids are Graph
/// mail-folder ids or well-known names such as <c>inbox</c>. Read key by key rather than through the
/// reflection-based configuration binder.
/// </summary>
/// <param name="FolderIds">Watched folders, de-duplicated; defaults to the inbox.</param>
/// <param name="MaxPerFolder">How many of the newest messages to fetch per folder (1–100).</param>
internal sealed record EmailOptions(IReadOnlyList<string> FolderIds, int MaxPerFolder)
{
    public const string SECTION = "Email";

    private const string DEFAULT_FOLDER = "inbox";

    public static EmailOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);

        List<string> folderIds = [.. section.GetSection(nameof(FolderIds)).GetChildren()
            .Select(c => c.Value?.Trim())
            .OfType<string>()
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (folderIds.Count == 0)
        {
            folderIds.Add(DEFAULT_FOLDER);
        }

        var maxPerFolder = int.TryParse(section[nameof(MaxPerFolder)], out var configured)
            ? Math.Clamp(configured, EmailSettings.PerFolderMin, EmailSettings.PerFolderMax)
            : EmailSettings.PerFolderDefault;

        return new EmailOptions(folderIds, maxPerFolder);
    }
}
