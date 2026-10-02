using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Email;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>One node of the folder tree. <see cref="Id"/> is null for the virtual "Favorites" entry.</summary>
public sealed partial class MailFolderViewModel : ObservableObject
{
    internal const string FAVORITES_NAME = "Favorites";

    private MailFolderViewModel(string? id, string name, int unreadCount, IReadOnlyList<MailFolderViewModel> children)
    {
        Id = id;
        Name = name;
        UnreadText = unreadCount > 0 ? unreadCount.ToString(CultureInfo.CurrentCulture) : "";
        Children = children;
    }

    /// <summary>The folders chosen in Settings → Email, merged newest first (the page's landing view).</summary>
    public static MailFolderViewModel Favorites() => new(null, FAVORITES_NAME, 0, []);

    public static MailFolderViewModel From(MailFolderNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return new(folder.Id, folder.DisplayName, folder.UnreadCount, [.. folder.Children.Select(From)]);
    }

    public string? Id { get; }

    public string Name { get; }

    /// <summary>Unread count, or empty when there is nothing unread.</summary>
    public string UnreadText { get; }

    public IReadOnlyList<MailFolderViewModel> Children { get; }

    /// <summary>Two-way bound to the tree item so a refresh can keep the user's expanded branches.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    public IEnumerable<MailFolderViewModel> SelfAndDescendants()
        => Children.SelectMany(c => c.SelfAndDescendants()).Prepend(this);
}
