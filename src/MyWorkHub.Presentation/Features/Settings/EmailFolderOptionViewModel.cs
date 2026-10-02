using CommunityToolkit.Mvvm.ComponentModel;
using MyWorkHub.Core.Features.Email;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>One tickable node of the Settings → Email folder tree. Ticking a folder does not tick its sub-folders.</summary>
public sealed partial class EmailFolderOptionViewModel : ObservableObject
{
    public EmailFolderOptionViewModel(MailFolderNode folder, IReadOnlySet<string> checkedIds)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(checkedIds);
        Id = folder.Id;
        Name = folder.DisplayName;
        _isChecked = checkedIds.Contains(folder.Id);
        Children = [.. folder.Children.Select(c => new EmailFolderOptionViewModel(c, checkedIds))];
    }

    public string Id { get; }

    public string Name { get; }

    public IReadOnlyList<EmailFolderOptionViewModel> Children { get; }

    [ObservableProperty]
    private bool _isChecked;

    public IEnumerable<EmailFolderOptionViewModel> SelfAndDescendants()
        => Children.SelectMany(c => c.SelfAndDescendants()).Prepend(this);
}
