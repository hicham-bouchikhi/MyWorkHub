using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Presentation.Features.GraphAuth;

namespace MyWorkHub.Presentation.Features.Teams;

/// <summary>Recently active Teams chats; a chat opens in Teams via its web link. Not a deep-link target.</summary>
public sealed partial class TeamsViewModel : GraphPageViewModel
{
    private readonly ITeamsService? _teams;
    private readonly IBrowserLauncher? _browser;

    public TeamsViewModel(
        ITeamsService? teams = null,
        IGraphConnectionService? connection = null,
        IBrowserLauncher? browser = null)
        : base("Teams", teams is not null, connection)
    {
        _teams = teams;
        _browser = browser;
    }

    /// <summary>Chats, most recent message first.</summary>
    public ObservableCollection<TeamsChatRowViewModel> Items { get; } = [];

    protected override async Task LoadDataAsync(CancellationToken ct)
    {
        var chats = await _teams!.GetRecentChatsAsync(ct);

        Items.Clear();
        foreach (var chat in chats)
        {
            Items.Add(new TeamsChatRowViewModel(chat));
        }
    }

    [RelayCommand]
    private void OpenChat(TeamsChatRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.CanOpen)
        {
            _browser?.Open(row.WebUrl);
        }
    }
}
