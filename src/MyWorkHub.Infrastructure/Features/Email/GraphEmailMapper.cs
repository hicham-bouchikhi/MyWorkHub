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
            message.BodyPreview ?? "",
            message.ReceivedDateTime?.UtcDateTime ?? DateTime.MinValue,
            message.Flag?.FlagStatus == FollowupFlagStatus.Flagged,
            message.IsRead ?? false,
            folderName);
    }
}
