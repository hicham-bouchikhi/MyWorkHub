using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

public sealed class TeamsViewModelTests
{
    private static TeamsChatItem Chat(string id, int unread) =>
        new(id, $"Chat {id}", "Alice", "Hello there", DateTime.UtcNow, unread, $"https://teams.microsoft.com/l/chat/{id}");

    [Fact]
    public async Task Refresh_loads_unread_chats()
    {
        var vm = new TeamsViewModel(new FakeTeamsService([Chat("1", 2), Chat("2", 1)]));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.IsEmpty);
        Assert.Equal(2, vm.Chats.Count);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Refresh_with_no_chats_sets_the_empty_flag()
    {
        var vm = new TeamsViewModel(new FakeTeamsService());

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Chats);
    }

    [Fact]
    public async Task Refresh_on_service_failure_sets_an_error_message()
    {
        var vm = new TeamsViewModel(new FakeTeamsService(throwOnCall: true));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Empty(vm.Chats);
        Assert.False(vm.IsEmpty); // the error message stands alone; no "No unread chats."
    }

    [Fact]
    public void Open_launches_the_chat_deep_link()
    {
        var launcher = new FakeBrowserLauncher();
        var vm = new TeamsViewModel(new FakeTeamsService(), launcher);
        var row = new TeamsChatRow(Chat("7", 3));

        vm.OpenCommand.Execute(row);

        Assert.Equal(row.DeepLinkUrl, launcher.LastUrl);
    }

    [Theory]
    [InlineData(1, "1 unread")]
    [InlineData(4, "4 unread")]
    public void Unread_count_text_is_humanised(int unread, string expected)
    {
        var row = new TeamsChatRow(Chat("1", unread));

        Assert.Equal(expected, row.UnreadCountText);
    }
}
