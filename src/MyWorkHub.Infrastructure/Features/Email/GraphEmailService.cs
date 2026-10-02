using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Infrastructure.Configuration;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary><see cref="IEmailService"/> over Microsoft Graph (<c>/me/mailFolders/{id}/messages</c>).</summary>
internal sealed partial class GraphEmailService : IEmailService
{
    // Ask Graph for a plain-text body, so no HTML ever reaches the UI.
    private const string PREFER_HEADER = "Prefer";
    private const string TEXT_BODY_PREFERENCE = "outlook.body-content-type=\"text\"";

    private const string CID_SCHEME = "cid:";
    private const string PLAIN_TEXT_STYLE = "white-space:pre-wrap;word-wrap:break-word;font-family:inherit;margin:0";
    private const int FOLDER_PAGE_SIZE = 100;

    // Outlook's order for the standard folders (Graph well-known names); every other folder follows, by name.
    private static readonly string[] _standardFolders =
        ["inbox", "drafts", "sentitems", "deleteditems", "junkemail", "archive", "outbox"];

    private static readonly string[] _folderFields = ["id", "displayName", "unreadItemCount", "childFolderCount"];

    private static readonly string[] _messageFields =
        ["id", "from", "subject", "bodyPreview", "receivedDateTime", "flag", "isRead"];

    private readonly GraphServiceClient _graph;
    private readonly LiveOptions<EmailOptions> _options;
    private readonly ILogger<GraphEmailService> _logger;

    public GraphEmailService(GraphServiceClient graph, LiveOptions<EmailOptions> options, ILogger<GraphEmailService> logger)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _graph = graph;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmailItem>> GetRecentEmailsAsync(CancellationToken ct = default)
    {
        var options = _options.Current;
        var emails = new List<EmailItem>();
        foreach (var folderId in options.FolderIds)
        {
            try
            {
                emails.AddRange(await ListFolderAsync(folderId, options.MaxPerFolder, ct).ConfigureAwait(false));
            }
            catch (ODataError ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
            {
                // A stale id in appsettings.json must not take the whole mail list down.
                LogFolderNotFound(folderId, ex);
            }
        }

        // The same folder can be configured twice under different ids (e.g. "inbox" and its real id).
        return emails
            .DistinctBy(e => e.Id)
            .OrderByDescending(e => e.ReceivedAt)
            .ToList();
    }

    public async Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var message = await _graph.Me.Messages[id].GetAsync(request =>
        {
            request.QueryParameters.Select = ["body"];
            request.Headers.Add(PREFER_HEADER, TEXT_BODY_PREFERENCE);
        }, ct).ConfigureAwait(false);

        // Outlook's HTML-to-text conversion still leaves entities (&amp;, &#39;, &nbsp;) undecoded.
        return EmailTextCleaner.CleanBody(WebUtility.HtmlDecode(message?.Body?.Content ?? ""));
    }

    public async Task<string> GetEmailHtmlAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var messageRequest = _graph.Me.Messages[id];
        var message = await messageRequest.GetAsync(
            request => request.QueryParameters.Select = ["body"], ct).ConfigureAwait(false);

        var body = message?.Body;
        if (body?.ContentType == BodyType.Text)
        {
            return $"<pre style=\"{PLAIN_TEXT_STYLE}\">{WebUtility.HtmlEncode(body.Content ?? "")}</pre>";
        }

        var html = body?.Content ?? "";
        if (!html.Contains(CID_SCHEME, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        // Inline pictures (signatures, logos, pasted screenshots) are attachments referenced as cid:{contentId};
        // the sandboxed reader loads nothing from outside the document, so they are embedded as data: URIs.
        var attachments = await messageRequest.Attachments.GetAsync(cancellationToken: ct).ConfigureAwait(false);
        foreach (var image in (attachments?.Value ?? []).OfType<FileAttachment>())
        {
            if (image is { IsInline: true, ContentId: { Length: > 0 } contentId, ContentBytes: { } bytes }
                && image.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                html = html.Replace(
                    CID_SCHEME + contentId,
                    $"data:{image.ContentType};base64,{Convert.ToBase64String(bytes)}",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        return html;
    }

    public async Task<IReadOnlyList<MailFolderNode>> GetFoldersAsync(CancellationToken ct = default)
    {
        var page = await _graph.Me.MailFolders.GetAsync(request =>
        {
            request.QueryParameters.Top = FOLDER_PAGE_SIZE;
            request.QueryParameters.Select = _folderFields;
        }, ct).ConfigureAwait(false);
        var nodes = await ToNodesAsync(page?.Value ?? [], ct).ConfigureAwait(false);

        // Graph lists folders alphabetically; Outlook shows its standard folders first, in a fixed order. Graph
        // v1.0 does not say which folder is which, so each well-known name is resolved to its id.
        var rankById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var rank = 0; rank < _standardFolders.Length; rank++)
        {
            if (await GetFolderIdAsync(_standardFolders[rank], ct).ConfigureAwait(false) is { } id)
            {
                rankById.TryAdd(id, rank);
            }
        }

        return nodes
            .OrderBy(n => rankById.TryGetValue(n.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(n => n.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<EmailItem>> GetFolderEmailsAsync(string folderId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);

        var emails = await ListFolderAsync(folderId, _options.Current.MaxPerFolder, ct).ConfigureAwait(false);
        return emails.OrderByDescending(e => e.ReceivedAt).ToList();
    }

    private async Task<IReadOnlyList<MailFolderNode>> ToNodesAsync(IEnumerable<MailFolder> folders, CancellationToken ct)
    {
        var nodes = new List<MailFolderNode>();
        foreach (var folder in folders.Where(f => !string.IsNullOrEmpty(f.Id)))
        {
            IReadOnlyList<MailFolderNode> children = [];
            if (folder.ChildFolderCount > 0)
            {
                var page = await _graph.Me.MailFolders[folder.Id].ChildFolders.GetAsync(request =>
                {
                    request.QueryParameters.Top = FOLDER_PAGE_SIZE;
                    request.QueryParameters.Select = _folderFields;
                }, ct).ConfigureAwait(false);
                children = await ToNodesAsync(page?.Value ?? [], ct).ConfigureAwait(false);
            }

            nodes.Add(new MailFolderNode(folder.Id!, folder.DisplayName ?? folder.Id!, folder.UnreadItemCount ?? 0, children));
        }

        return nodes;
    }

    public async Task<string?> GetFolderIdAsync(string idOrWellKnownName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrWellKnownName);
        try
        {
            var folder = await _graph.Me.MailFolders[idOrWellKnownName].GetAsync(
                request => request.QueryParameters.Select = ["id"], ct).ConfigureAwait(false);
            return folder?.Id;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<IEnumerable<EmailItem>> ListFolderAsync(string folderId, int maxPerFolder, CancellationToken ct)
    {
        var folderRequest = _graph.Me.MailFolders[folderId];

        var folder = await folderRequest.GetAsync(
            request => request.QueryParameters.Select = ["displayName"], ct).ConfigureAwait(false);

        var page = await folderRequest.Messages.GetAsync(request =>
        {
            request.QueryParameters.Top = maxPerFolder;
            request.QueryParameters.Select = _messageFields;
            request.QueryParameters.Orderby = ["receivedDateTime desc"];
        }, ct).ConfigureAwait(false);

        var folderName = folder?.DisplayName ?? folderId;
        return (page?.Value ?? []).Select(m => GraphEmailMapper.ToEmailItem(m, folderName));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Watched mail folder '{FolderId}' does not exist; skipping it.")]
    private partial void LogFolderNotFound(string folderId, Exception exception);
}
