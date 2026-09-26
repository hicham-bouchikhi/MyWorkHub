using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.Teams;
using MyWorkHub.Presentation.Features.Teams;
using MyWorkHub.UI.Tests.Features.GraphAuth;

namespace MyWorkHub.UI.Tests.Features.Teams;

public sealed class TeamsViewModelTests
{
    private static TeamsChatItem Chat(string title, string url = "https://teams.microsoft.com/l/chat/1")
        => new("id-" + title, title, "see you", "Bob", new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc), true, url);

    private sealed class RecordingBrowser : IBrowserLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string url) => Opened.Add(url);
    }

    [Fact]
    public async Task Should_show_the_recent_chats_with_a_sender_prefixed_preview()
    {
        var connection = new FakeGraphConnection();
        var viewModel = new TeamsViewModel(new FakeTeamsService(connection, Chat("Release crew")), connection);

        await viewModel.LoadCommand.ExecuteAsync(null);

        var row = Assert.Single(viewModel.Items);
        Assert.Equal("Release crew", row.Title);
        Assert.Equal("Bob: see you", row.Preview);
        Assert.True(row.IsUnread);
    }

    [Fact]
    public async Task Should_open_the_chat_in_teams()
    {
        var connection = new FakeGraphConnection();
        var browser = new RecordingBrowser();
        var viewModel = new TeamsViewModel(new FakeTeamsService(connection, Chat("A"), Chat("B", url: "")), connection, browser);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.OpenChatCommand.Execute(viewModel.Items[0]);
        viewModel.OpenChatCommand.Execute(viewModel.Items[1]);

        Assert.Equal(["https://teams.microsoft.com/l/chat/1"], browser.Opened);
        Assert.False(viewModel.Items[1].CanOpen);
    }

    [Fact]
    public async Task Should_offer_sign_in_when_not_connected()
    {
        var connection = new FakeGraphConnection { IsSignedIn = false };
        var viewModel = new TeamsViewModel(new FakeTeamsService(connection), connection);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.True(viewModel.NeedsSignIn);
    }
}
