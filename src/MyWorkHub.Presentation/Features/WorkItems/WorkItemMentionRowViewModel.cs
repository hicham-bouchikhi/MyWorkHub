using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Presentation.Features.WorkItems;

/// <summary>One @mention of the user in a work item comment. Mutable only in its read state.</summary>
public sealed partial class WorkItemMentionRowViewModel : ObservableObject
{
    private const string CREATED_FORMAT = "ddd d MMM HH:mm";

    public WorkItemMentionRowViewModel(WorkItemMention mention, bool isUnread)
    {
        ArgumentNullException.ThrowIfNull(mention);
        CommentId = mention.CommentId;
        WorkItemId = mention.WorkItemId;
        WorkItemUrl = mention.WorkItemUrl;
        Author = mention.AuthorDisplayName;
        Snippet = mention.TextSnippet;
        WorkItemText = $"#{mention.WorkItemId.ToString(CultureInfo.InvariantCulture)} {mention.WorkItemTitle}";
        CreatedAtText = mention.CreatedAt.ToLocalTime().ToString(CREATED_FORMAT, CultureInfo.CurrentCulture);
        _isUnread = isUnread;
    }

    public int CommentId { get; }

    public int WorkItemId { get; }

    public string WorkItemUrl { get; }

    public string Author { get; }

    public string Snippet { get; }

    /// <summary>e.g. <c>#1234 Fix the login page</c>.</summary>
    public string WorkItemText { get; }

    /// <summary>When the comment was posted, in the user's local time zone and culture.</summary>
    public string CreatedAtText { get; }

    /// <summary>True until the user opens the mention (persisted across sessions).</summary>
    [ObservableProperty]
    private bool _isUnread;
}
