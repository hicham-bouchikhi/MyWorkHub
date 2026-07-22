using MyWorkHub.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MyWorkHub.UI.ViewModels;

/// <summary>A work item @mention projected for the Mentions list.</summary>
public sealed partial class WorkItemMentionRow : ObservableObject
{
    private WorkItemMentionRow(WorkItemMention mention, bool isNew)
    {
        CommentId = mention.CommentId;
        WorkItemTitle = mention.WorkItemTitle;
        AuthorDisplayName = mention.AuthorDisplayName;
        TextSnippet = mention.TextSnippet;
        Url = mention.WorkItemUrl;
        _isNew = isNew;
        When = FormatRelativeTime(mention.CreatedAt);
    }

    public int CommentId { get; }

    public string WorkItemTitle { get; }

    public string AuthorDisplayName { get; }

    public string TextSnippet { get; }

    public string Url { get; }

    public string When { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSeen))]
    private bool _isNew;

    public bool IsSeen => !IsNew;

    /// <summary>Marks this mention as read without persisting (caller handles persistence).</summary>
    public void MarkSeen() => IsNew = false;

    public static WorkItemMentionRow From(WorkItemMention mention, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(mention);
        return new WorkItemMentionRow(mention, isNew);
    }

    private static string FormatRelativeTime(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        return diff.TotalMinutes < 1 ? "just now"
             : diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes}m ago"
             : diff.TotalDays < 1 ? $"{(int)diff.TotalHours}h ago"
             : diff.TotalDays < 7 ? $"{(int)diff.TotalDays}d ago"
             : utc.ToLocalTime().ToString("d", System.Globalization.CultureInfo.CurrentCulture);
    }
}
