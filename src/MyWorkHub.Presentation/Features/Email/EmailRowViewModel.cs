using System.Globalization;
using MyWorkHub.Core.Features.Email;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>One row of the mail list (immutable; a refresh replaces the rows).</summary>
public sealed class EmailRowViewModel
{
    private const string RECEIVED_FORMAT = "ddd d MMM HH:mm";

    public EmailRowViewModel(EmailItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        Id = item.Id;
        From = item.From;
        Subject = string.IsNullOrWhiteSpace(item.Subject) ? "(no subject)" : item.Subject;
        Preview = item.Preview;
        FolderName = item.FolderName;
        IsFlagged = item.IsFlagged;
        IsUnread = !item.IsRead;
        ReceivedAtText = item.ReceivedAt.ToLocalTime().ToString(RECEIVED_FORMAT, CultureInfo.CurrentCulture);
    }

    /// <summary>The message header this row shows (what a single-email summary is built from).</summary>
    public EmailItem Item { get; }

    public string Id { get; }

    public string From { get; }

    public string Subject { get; }

    public string Preview { get; }

    public string FolderName { get; }

    public bool IsFlagged { get; }

    public bool IsUnread { get; }

    /// <summary>Arrival time in the user's local time zone and culture.</summary>
    public string ReceivedAtText { get; }
}
