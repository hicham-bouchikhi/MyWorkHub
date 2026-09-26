using Microsoft.Graph;
using MyWorkHub.Core.Features.Teams;

namespace MyWorkHub.Infrastructure.Features.Teams;

/// <summary><see cref="ITeamsService"/> over Microsoft Graph <c>/me/chats</c> (chats only, never channel posts).</summary>
internal sealed class GraphTeamsService : ITeamsService
{
    // /me/chats caps $top at 50; paging further back is not needed for a "recent chats" list.
    private const int CHAT_PAGE_SIZE = 50;

    private readonly GraphServiceClient _graph;

    public GraphTeamsService(GraphServiceClient graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public async Task<IReadOnlyList<TeamsChatItem>> GetRecentChatsAsync(CancellationToken ct = default)
    {
        var response = await _graph.Me.Chats.GetAsync(request =>
        {
            request.QueryParameters.Expand = ["lastMessagePreview"];
            request.QueryParameters.Orderby = ["lastMessagePreview/createdDateTime desc"];
            request.QueryParameters.Top = CHAT_PAGE_SIZE;
        }, ct).ConfigureAwait(false);

        return (response?.Value ?? [])
            .Select(GraphTeamsMapper.ToTeamsChatItem)
            .ToList();
    }
}
