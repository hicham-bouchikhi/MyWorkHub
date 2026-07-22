namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Delegated Microsoft Graph scopes requested at sign-in (ARCHITECTURE.md §8).
/// Reserved scopes (openid/profile/offline_access) are added by MSAL automatically
/// and must not be listed here.
///
/// NOTE: <c>ChannelMessage.Read.All</c> is deliberately NOT requested here. It requires
/// *admin* consent in the Cegid tenant, and because MSAL asks for every scope in a single
/// interactive call, including it forces an "admin approval required" wall on the whole
/// sign-in — blocking Mail/Calendar/Chat too. The Teams module degrades gracefully without
/// it (channel @mentions unavailable; DMs/group chats still work via Chat.Read — see T076).
/// If channel reading is ever needed, request it incrementally after an admin grants it.
/// </summary>
public static class GraphScopes
{
    public static IReadOnlyList<string> Delegated { get; } =
    [
        "Mail.Read",
        "Calendars.ReadWrite",
        "Chat.Read",
        "User.Read",
        // "ChannelMessage.Read.All" // NOTE: This scope is commented out to avoid admin consent issues. Uncomment if needed and admin consent is granted.
    ];
}
