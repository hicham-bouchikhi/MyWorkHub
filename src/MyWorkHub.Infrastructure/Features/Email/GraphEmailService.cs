using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using MyWorkHub.Core.Features.Email;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary><see cref="IEmailService"/> over Microsoft Graph (<c>/me/mailFolders/{id}/messages</c>).</summary>
internal sealed partial class GraphEmailService : IEmailService
{
    // Ask Graph for a plain-text body, so no HTML ever reaches the UI.
    private const string PREFER_HEADER = "Prefer";
    private const string TEXT_BODY_PREFERENCE = "outlook.body-content-type=\"text\"";

    private static readonly string[] _messageFields =
        ["id", "from", "subject", "bodyPreview", "receivedDateTime", "flag", "isRead"];

    private readonly GraphServiceClient _graph;
    private readonly EmailOptions _options;
    private readonly ILogger<GraphEmailService> _logger;

    public GraphEmailService(GraphServiceClient graph, EmailOptions options, ILogger<GraphEmailService> logger)
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
        var emails = new List<EmailItem>();
        foreach (var folderId in _options.FolderIds)
        {
            try
            {
                emails.AddRange(await GetFolderEmailsAsync(folderId, ct).ConfigureAwait(false));
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
        return WebUtility.HtmlDecode(message?.Body?.Content ?? "");
    }

    private async Task<IEnumerable<EmailItem>> GetFolderEmailsAsync(string folderId, CancellationToken ct)
    {
        var folderRequest = _graph.Me.MailFolders[folderId];

        var folder = await folderRequest.GetAsync(
            request => request.QueryParameters.Select = ["displayName"], ct).ConfigureAwait(false);

        var page = await folderRequest.Messages.GetAsync(request =>
        {
            request.QueryParameters.Top = _options.MaxPerFolder;
            request.QueryParameters.Select = _messageFields;
            request.QueryParameters.Orderby = ["receivedDateTime desc"];
        }, ct).ConfigureAwait(false);

        var folderName = folder?.DisplayName ?? folderId;
        return (page?.Value ?? []).Select(m => GraphEmailMapper.ToEmailItem(m, folderName));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Watched mail folder '{FolderId}' does not exist; skipping it.")]
    private partial void LogFolderNotFound(string folderId, Exception exception);
}
