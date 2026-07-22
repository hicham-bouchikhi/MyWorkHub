using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// <see cref="IEmailSettingsService"/>: persists the watched mail folders to the user's
/// appsettings.json. Shares the one <see cref="EmailOptions"/> instance (via
/// <see cref="IOptions{T}"/>) with <see cref="GraphEmailService"/>, so mutating it here makes
/// the new folder set take effect immediately — no app restart. Mirrors
/// <see cref="CodeReview.WorkspaceSettingsService"/>.
/// </summary>
public sealed class EmailSettingsService : IEmailSettingsService
{
    private readonly EmailOptions _options;

    public EmailSettingsService(IOptions<EmailOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public IReadOnlyList<string> GetWatchedFolderIds() => _options.FolderIds.ToList();

    public void Save(IReadOnlyList<string> folderIds)
    {
        ArgumentNullException.ThrowIfNull(folderIds);

        // Update the live (shared) options so GraphEmailService sees the change at once.
        _options.FolderIds.Clear();
        foreach (var id in folderIds)
        {
            _options.FolderIds.Add(id);
        }

        // Persist for the next launch.
        UserAppSettingsFile.SaveEmailFolders(folderIds);
    }
}
