using MyWorkHub.Core.Models;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>Projects a Graph <see cref="Message"/> onto the <see cref="EmailItem"/> read model.</summary>
public static class GraphEmailMapper
{
    public static EmailItem ToEmailItem(Message message, string folderName = "")
    {
        ArgumentNullException.ThrowIfNull(message);

        var address = message.From?.EmailAddress;
        var from = address?.Name is { Length: > 0 } name
            ? name
            : address?.Address is { Length: > 0 } addr ? addr : "(unknown sender)";

        var isFlagged = message.Flag?.FlagStatus == FollowupFlagStatus.Flagged;

        return new EmailItem(
            Id: message.Id ?? "",
            From: from,
            Subject: message.Subject is { Length: > 0 } subject ? subject : "(no subject)",
            Preview: message.BodyPreview ?? "",
            ReceivedAt: message.ReceivedDateTime?.UtcDateTime ?? default,
            IsFlagged: isFlagged,
            FolderName: folderName);
    }
}
