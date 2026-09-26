using MyWorkHub.Core.Features.GraphAuth;

namespace MyWorkHub.Core.Features.Teams;

/// <summary>
/// Read access to the user's Teams chats (one-on-one, group and meeting chats — never channel posts,
/// which would need an admin-consented scope). Throws <see cref="GraphNotConnectedException"/> when
/// the user is not signed in to Microsoft 365.
/// </summary>
public interface ITeamsService
{
    /// <summary>The most recently active chats, newest message first.</summary>
    Task<IReadOnlyList<TeamsChatItem>> GetRecentChatsAsync(CancellationToken ct = default);
}
