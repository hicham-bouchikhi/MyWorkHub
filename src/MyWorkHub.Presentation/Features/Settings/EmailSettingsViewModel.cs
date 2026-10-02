using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Features.Settings;
using MyWorkHub.Presentation.ViewModels;

namespace MyWorkHub.Presentation.Features.Settings;

/// <summary>
/// Settings → Email: whether the Email page shows Favorites (hidden by default), which mailbox folders make it up (ticked in the real folder tree,
/// saved by Graph id to <c>Email:FolderIds</c>) and how many messages to take from each. The Email page reads the
/// section on every load, so a save applies on its next refresh. Configured well-known names (the default
/// <c>inbox</c>) are resolved to their folder so they show ticked. When the mailbox cannot be read (signed out,
/// offline), the tree is replaced by a notice and a save keeps the configured folders as they are.
/// </summary>
public sealed partial class EmailSettingsViewModel : ViewModelBase
{
    private readonly ISettingsService? _settings;
    private readonly IEmailService? _email;
    private IReadOnlyList<string> _configuredIds = [];

    public EmailSettingsViewModel(ISettingsService? settings = null, IEmailService? email = null)
    {
        _settings = settings;
        _email = email;
    }

    /// <summary>False when settings cannot be persisted; the panel then shows a notice.</summary>
    public bool IsAvailable => _settings is not null;

    /// <summary>The mailbox's folders; empty until <see cref="LoadFoldersCommand"/> succeeds.</summary>
    public ObservableCollection<EmailFolderOptionViewModel> Folders { get; } = [];

    [ObservableProperty]
    private int _maxPerFolder = EmailSettings.PerFolderDefault;

    /// <summary>Whether the Email page shows Favorites atop its folder tree.</summary>
    [ObservableProperty]
    private bool _showFavorites;

    /// <summary>Why the folder tree is not shown (signed out, mailbox unreachable); null when it is.</summary>
    [ObservableProperty]
    private string? _folderNotice;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public void Load()
    {
        if (_settings is not null)
        {
            var email = _settings.GetEmail();
            _configuredIds = email.FolderIds;
            MaxPerFolder = email.MaxPerFolder;
            ShowFavorites = email.ShowFavorites;
        }

        StatusMessage = null;
        ErrorMessage = null;
    }

    /// <summary>Reads the mailbox tree and ticks the configured folders.</summary>
    [RelayCommand]
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        Folders.Clear();
        if (_email is null)
        {
            FolderNotice = "Microsoft 365 is not available, so folders cannot be listed.";
            return;
        }

        try
        {
            var tree = await _email.GetFoldersAsync(ct);
            var known = tree.SelectMany(Flatten).Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
            var checkedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in _configuredIds.DefaultIfEmpty("inbox"))
            {
                if (known.Contains(id))
                {
                    checkedIds.Add(id);
                }
                else if (await _email.GetFolderIdAsync(id, ct) is { } resolved)
                {
                    checkedIds.Add(resolved);
                }
            }

            foreach (var folder in tree)
            {
                Folders.Add(new EmailFolderOptionViewModel(folder, checkedIds));
            }

            FolderNotice = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded by a newer load (the page was shown again); that run fills the tree.
        }
        catch (GraphNotConnectedException)
        {
            FolderNotice = "Sign in to Microsoft 365 on the Email page to choose folders. Saving keeps the current folders.";
        }
        catch (Exception ex)
        {
            FolderNotice = $"Could not read your mail folders: {ex.Message}. Saving keeps the current folders.";
        }
    }

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private async Task SaveAsync(CancellationToken ct)
    {
        IReadOnlyList<string> folderIds = Folders.Count > 0
            ? [.. Folders.SelectMany(f => f.SelfAndDescendants()).Where(f => f.IsChecked).Select(f => f.Id)]
            : _configuredIds;
        var settings = new EmailSettings(folderIds, Math.Clamp(MaxPerFolder, EmailSettings.PerFolderMin, EmailSettings.PerFolderMax), ShowFavorites);
        try
        {
            await _settings!.SaveEmailAsync(settings, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = $"Could not save the Email settings: {ex.Message}";
            StatusMessage = null;
            return;
        }

        _configuredIds = settings.FolderIds;
        MaxPerFolder = settings.MaxPerFolder;
        ErrorMessage = null;
        StatusMessage = folderIds.Count == 0
            ? "Saved. No folder is ticked, so Favorites shows the inbox. Press Refresh on the Email page to apply."
            : "Saved. Press Refresh on the Email page to apply.";
    }

    private static IEnumerable<MailFolderNode> Flatten(MailFolderNode folder) => folder.Children.SelectMany(Flatten).Prepend(folder);
}
