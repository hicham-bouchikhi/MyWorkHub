using System.Net;
using Microsoft.Graph.Models;
using MyWorkHub.Core.Features.Email;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary>Graph <see cref="Message"/> → <see cref="EmailItem"/>. Missing fields degrade to empty values.</summary>
internal static class GraphEmailMapper
{
    public static EmailItem ToEmailItem(Message message, string folderName)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sender = message.From?.EmailAddress;
        var from = !string.IsNullOrWhiteSpace(sender?.Name) ? sender.Name : sender?.Address ?? "";

        return new EmailItem(
            message.Id ?? "",
            from,
            message.Subject ?? "",
            // bodyPreview is Graph's own plain-text truncation, but it still leaves HTML entities
            // (&amp;, &#39;, &nbsp;) undecoded for messages that originated as HTML -- decode them so the
            // list doesn't show literal entity codes instead of the punctuation/spaces they represent.
            // The list shows it as a short summary, so it is flattened to one line without link targets.
            EmailTextCleaner.CleanPreview(WebUtility.HtmlDecode(message.BodyPreview ?? "")),
            message.ReceivedDateTime?.UtcDateTime ?? DateTime.MinValue,
            message.Flag?.FlagStatus == FollowupFlagStatus.Flagged,
            message.IsRead ?? false,
            folderName);
    }
}
