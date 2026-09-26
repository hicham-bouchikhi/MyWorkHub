namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// Delegated Microsoft Graph scopes requested at sign-in. The reserved OIDC scopes (openid, profile,
/// offline_access) are added by MSAL and must not be listed.
/// <para>
/// Only user-consentable scopes: MSAL requests all of them in one interactive call, so a single
/// admin-only scope (e.g. <c>ChannelMessage.Read.All</c>) would block the whole sign-in behind an
/// "admin approval required" wall. Teams therefore reads chats only, never channel posts.
/// </para>
/// <para>
/// <c>Calendars.ReadWrite</c> (not <c>.Read</c>) matches the scope already granted to the previous
/// version of this app. Narrowing it would force re-consent and a new admin-approval wait for a scope
/// that was already working -- keep it as-is until write access is actually used (remote-work sync).
/// </para>
/// </summary>
internal static class GraphScopes
{
    public static IReadOnlyList<string> Delegated { get; } =
    [
        "User.Read",
        "Mail.Read",
        "Calendars.ReadWrite",
        "Chat.Read",
    ];
}
