using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary><see cref="IEmailService"/> backed by Microsoft Graph (T013).</summary>
public sealed class GraphEmailService : IEmailService
{
    // Folder-list projection: enough to render the picker and know whether to recurse.
    private static readonly string[] _folderSelectFields = ["id", "displayName", "unreadItemCount", "childFolderCount"];

    // Graph well-known folder names. A configured folder id that matches one of these is a
    // seed/default (Graph accepts it for addressing) that must be resolved to its opaque id
    // so the picker can pre-check it — the folder list only returns opaque ids.
    private static readonly HashSet<string> _wellKnownFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "inbox", "drafts", "sentitems", "deleteditems", "junkemail",
        "archive", "clutter", "outbox", "conversationhistory",
    };

    private readonly GraphServiceClient _graph;
    private readonly EmailOptions _options;

    public GraphEmailService(GraphServiceClient graph, IOptions<EmailOptions> options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        _graph = graph;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default)
    {
        var folderIds = GraphEmailQuery.WatchedFolderIds(_options.FolderIds);

        // One filtered page per watched folder, fetched in parallel, then merged/deduped/sorted.
        var perFolder = await Task.WhenAll(
            folderIds.Select(folderId => FetchFolderAsync(folderId, ct))).ConfigureAwait(false);

        return GraphEmailAggregator.Merge(perFolder);
    }

    private async Task<IReadOnlyList<EmailItem>> FetchFolderAsync(string folderId, CancellationToken ct)
    {
        // Fetch the folder display name and messages in parallel — no added latency.
        // NOTE: no server-side $orderby on messages. The "important" filter is an OR across two
        // different properties (isRead, flag/flagStatus), so Graph rejects a combined
        // $filter+$orderby with "The restriction or sort order is too complex for this operation."
        // Sorting happens in GraphEmailAggregator instead.
        var folderTask = _graph.Me.MailFolders[folderId]
            .GetAsync(req => req.QueryParameters.Select = ["displayName"], ct);

        var messagesTask = _graph.Me.MailFolders[folderId].Messages.GetAsync(request =>
        {
            request.QueryParameters.Filter = GraphEmailQuery.ImportantMessagesFilter;
            request.QueryParameters.Top = _options.MaxPerFolder;
            request.QueryParameters.Select = [.. GraphEmailQuery.SelectFields];
        }, ct);

        await Task.WhenAll(folderTask, messagesTask).ConfigureAwait(false);

        var folder = await folderTask.ConfigureAwait(false);
        var messageResponse = await messagesTask.ConfigureAwait(false);
        var folderName = folder?.DisplayName ?? "";
        var messages = messageResponse?.Value ?? [];
        return messages.Select(m => GraphEmailMapper.ToEmailItem(m, folderName)).ToList();
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var folderIds = GraphEmailQuery.WatchedFolderIds(_options.FolderIds);

        // Graph's unreadItemCount is per-folder (it does not include sub-folders), so summing
        // across every watched folder is the total the dashboard should show — not just Inbox.
        var counts = await Task.WhenAll(
            folderIds.Select(folderId => FetchUnreadCountAsync(folderId, ct))).ConfigureAwait(false);

        return counts.Sum();
    }

    private async Task<int> FetchUnreadCountAsync(string folderId, CancellationToken ct)
    {
        var folder = await _graph.Me.MailFolders[folderId]
            .GetAsync(request => request.QueryParameters.Select = ["unreadItemCount"], ct)
            .ConfigureAwait(false);
        return folder?.UnreadItemCount ?? 0;
    }

    public async Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default)
    {
        var watched = new HashSet<string>(_options.FolderIds, StringComparer.Ordinal);

        // Resolve well-known-name seeds (e.g. "inbox") to their opaque ids so the picker
        // pre-checks the right folder. Well-known folders always exist, so no 404 guard.
        foreach (var name in _options.FolderIds.Where(_wellKnownFolderNames.Contains))
        {
            var resolved = await _graph.Me.MailFolders[name]
                .GetAsync(request => request.QueryParameters.Select = ["id"], ct)
                .ConfigureAwait(false);
            if (resolved?.Id is { Length: > 0 } id)
            {
                watched.Add(id);
            }
        }

        var response = await _graph.Me.MailFolders.GetAsync(request =>
        {
            request.QueryParameters.Top = 100;
            request.QueryParameters.Select = _folderSelectFields;
        }, ct).ConfigureAwait(false);

        // /me/mailFolders returns only top-level folders. Walk each folder's children so the
        // picker also shows folders the user nested (e.g. custom folders under Inbox), as a tree.
        return await BuildTreeAsync(response?.Value, watched, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<MailFolder>> BuildTreeAsync(
        List<Microsoft.Graph.Models.MailFolder>? folders,
        ISet<string> watched,
        CancellationToken ct)
    {
        if (folders is null)
        {
            return [];
        }

        var result = new List<MailFolder>(folders.Count);
        foreach (var folder in folders)
        {
            var mapped = GraphMailFolderMapper.ToMailFolder(folder, watched);

            IReadOnlyList<MailFolder> children = [];
            if ((folder.ChildFolderCount ?? 0) > 0 && folder.Id is { Length: > 0 } folderId)
            {
                var childResponse = await _graph.Me.MailFolders[folderId].ChildFolders.GetAsync(request =>
                {
                    request.QueryParameters.Top = 100;
                    request.QueryParameters.Select = _folderSelectFields;
                }, ct).ConfigureAwait(false);

                children = await BuildTreeAsync(childResponse?.Value, watched, ct).ConfigureAwait(false);
            }

            result.Add(mapped with { Children = children });
        }

        return result;
    }

    public async Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var message = await _graph.Me.Messages[id]
            .GetAsync(req => req.QueryParameters.Select = ["body"], ct)
            .ConfigureAwait(false);

        var html = message?.Body?.Content;
        return string.IsNullOrEmpty(html) ? string.Empty : HtmlBodyExtractor.StripTags(html);
    }

    public Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default)
        => throw new NotImplementedException("SendEmailAsync is implemented in Phase 10 (TASKS.md T055).");
}
