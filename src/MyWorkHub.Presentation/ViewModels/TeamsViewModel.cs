using System.Collections.ObjectModel;
using System.Globalization;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace MyWorkHub.UI.ViewModels;

/// <summary>
/// Teams digest (Phase 5b, T077–T078): lists the user's unread conversations from
/// <see cref="ITeamsService"/> and opens a selected chat in Teams via its deep link.
/// </summary>
public sealed partial class TeamsViewModel : PageViewModel
{
    private readonly ITeamsService? _teamsService;
    private readonly IBrowserLauncher? _launcher;
    private readonly ILogger<TeamsViewModel>? _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string? _errorMessage;

    public TeamsViewModel(
        ITeamsService? teamsService = null,
        IBrowserLauncher? launcher = null,
        ILogger<TeamsViewModel>? logger = null)
        : base("Teams")
    {
        _teamsService = teamsService;
        _launcher = launcher;
        _logger = logger;
    }

    /// <summary>Unread conversations, most recent first.</summary>
    public ObservableCollection<TeamsChatRow> Chats { get; } = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        ErrorMessage = null;
        IsEmpty = false;

        if (_teamsService is null)
        {
            Chats.Clear();
            IsEmpty = true;
            return;
        }

        IsLoading = true;
        try
        {
            var items = await _teamsService.GetUnreadChatsAsync().ConfigureAwait(true);

            Chats.Clear();
            foreach (var item in items)
            {
                Chats.Add(new TeamsChatRow(item));
            }

            IsEmpty = Chats.Count == 0;
        }
        catch (Exception ex)
        {
            LogLoadFailure(ex);
            Chats.Clear();
            ErrorMessage = "Couldn't load your Teams chats. Make sure Microsoft 365 is connected in Settings.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Open(TeamsChatRow? row)
    {
        if (row is not null)
        {
            _launcher?.Open(row.DeepLinkUrl);
        }
    }

    private void LogLoadFailure(Exception exception)
    {
        if (_logger is not null)
        {
            LogLoadFailureCore(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load the Teams chat digest.")]
    private static partial void LogLoadFailureCore(ILogger logger, Exception exception);
}

/// <summary>A single unread Teams conversation projected for the digest list (display-only).</summary>
public sealed class TeamsChatRow
{
    public TeamsChatRow(TeamsChatItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        ChatName = item.ChatName;
        LastSenderName = item.LastSenderName;
        MessagePreview = item.MessagePreview;
        ReceivedAt = item.ReceivedAt.ToString("dd MMM HH:mm", CultureInfo.CurrentCulture);
        UnreadCount = item.UnreadCount;
        DeepLinkUrl = item.DeepLinkUrl;
    }

    public string ChatName { get; }

    public string LastSenderName { get; }

    public string MessagePreview { get; }

    public string ReceivedAt { get; }

    public int UnreadCount { get; }

    public string UnreadCountText => $"{UnreadCount.ToString(CultureInfo.CurrentCulture)} unread";

    public bool HasUnread => UnreadCount > 0;

    public string DeepLinkUrl { get; }
}
