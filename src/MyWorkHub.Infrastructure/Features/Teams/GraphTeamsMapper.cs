using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Graph.Models;
using MyWorkHub.Core.Features.Teams;

namespace MyWorkHub.Infrastructure.Features.Teams;

/// <summary>Graph <see cref="Chat"/> (with <c>lastMessagePreview</c> expanded) → <see cref="TeamsChatItem"/>.</summary>
internal static partial class GraphTeamsMapper
{
    private const string UNTITLED_CHAT = "Chat";

    public static TeamsChatItem ToTeamsChatItem(Chat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        var preview = chat.LastMessagePreview;
        var senderName = preview?.From?.User?.DisplayName ?? "";
        var lastMessageAt = preview?.CreatedDateTime?.UtcDateTime;
        var lastReadAt = chat.Viewpoint?.LastMessageReadDateTime?.UtcDateTime;

        // One-on-one chats have no topic; the latest sender is the best cheap label (members would
        // need a second expansion).
        var title = !string.IsNullOrWhiteSpace(chat.Topic) ? chat.Topic
            : senderName.Length > 0 ? senderName
            : UNTITLED_CHAT;

        return new TeamsChatItem(
            chat.Id ?? "",
            title,
            ToPlainText(preview?.Body?.Content),
            senderName,
            lastMessageAt,
            lastMessageAt is { } sent && (lastReadAt is null || sent > lastReadAt),
            chat.WebUrl ?? "");
    }

    /// <summary>Teams message bodies are HTML: drop the tags, decode entities and collapse whitespace.</summary>
    internal static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        var withoutTags = TagPattern().Replace(html, " ");
        return WhitespacePattern().Replace(WebUtility.HtmlDecode(withoutTags), " ").Trim();
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
