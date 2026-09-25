using System.Globalization;
using MyWorkHub.Core.Features.Teams;

namespace MyWorkHub.Presentation.Features.Teams;

/// <summary>One recent chat, formatted for display.</summary>
public sealed class TeamsChatRowViewModel
{
    private const string TIME_FORMAT = "ddd d MMM HH:mm";

    public TeamsChatRowViewModel(TeamsChatItem chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        Title = chat.Title;
        Preview = chat.LastSenderName.Length > 0 && chat.LastMessagePreview.Length > 0
            ? $"{chat.LastSenderName}: {chat.LastMessagePreview}"
            : chat.LastMessagePreview;
        LastMessageText = chat.LastMessageAt?.ToLocalTime().ToString(TIME_FORMAT, CultureInfo.CurrentCulture) ?? "";
        IsUnread = chat.IsUnread;
        WebUrl = chat.WebUrl;
    }

    public string Title { get; }

    /// <summary>"Sender: message" excerpt of the latest message.</summary>
    public string Preview { get; }

    public string LastMessageText { get; }

    public bool IsUnread { get; }

    public string WebUrl { get; }

    public bool CanOpen => WebUrl.Length > 0;
}
