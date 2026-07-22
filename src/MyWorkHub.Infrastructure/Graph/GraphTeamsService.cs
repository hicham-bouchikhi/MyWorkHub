using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// <see cref="ITeamsService"/> backed by Microsoft Graph (T075, T076). Calls
/// <c>GET /me/chats</c> (expanded with <c>lastMessagePreview</c>) and returns the unread
/// conversations — DMs and group chats only, since <c>/me/chats</c> never includes channel
/// posts. Channel @mentions would need <c>ChannelMessage.Read.All</c> (admin consent, not
/// requested — see <see cref="GraphScopes"/>); their absence is the expected degraded mode.
///
/// Graceful degradation (T076): any Graph failure — including a missing-consent
/// <c>403</c> — is logged and swallowed; the methods never throw, so the dashboard and
/// Teams page keep working with whatever is available (empty when nothing is).
/// </summary>
public sealed partial class GraphTeamsService : ITeamsService
{
    // /me/chats caps $top at 50 (Graph "List chats" docs); paging is not pursued for the digest.
    private const int CHAT_PAGE_SIZE = 50;

    private readonly GraphServiceClient _graph;
    private readonly ILogger<GraphTeamsService>? _logger;

    public GraphTeamsService(GraphServiceClient graph, ILogger<GraphTeamsService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _graph.Me.Chats.GetAsync(request =>
            {
                request.QueryParameters.Expand = ["lastMessagePreview"];
                request.QueryParameters.Orderby = ["lastMessagePreview/createdDateTime desc"];
                request.QueryParameters.Top = CHAT_PAGE_SIZE;
            }, ct).ConfigureAwait(false);

            var chats = response?.Value ?? [];
            return chats
                .Where(GraphTeamsMapper.IsUnread)
                .Select(GraphTeamsMapper.ToTeamsChatItem)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDegraded(ex);
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        // Lightweight dashboard counter. Graph has no per-chat unreadMessageCount, so this counts
        // unread *conversations* (one each). It deliberately skips the orderby and the read-model
        // projection that GetUnreadChatsAsync does — see ARCHITECTURE.md §12 / TASKS.md T075.
        try
        {
            var response = await _graph.Me.Chats.GetAsync(request =>
            {
                request.QueryParameters.Expand = ["lastMessagePreview"];
                request.QueryParameters.Top = CHAT_PAGE_SIZE;
            }, ct).ConfigureAwait(false);

            var chats = response?.Value ?? [];
            return chats.Count(GraphTeamsMapper.IsUnread);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDegraded(ex);
            return 0;
        }
    }

    private void LogDegraded(Exception exception)
    {
        if (_logger is not null)
        {
            LogDegradedCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read Teams chats from Microsoft Graph; degrading to no unread conversations.")]
    private static partial void LogDegradedCore(ILogger logger, Exception exception);
}
