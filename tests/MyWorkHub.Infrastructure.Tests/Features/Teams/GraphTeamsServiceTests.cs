using Microsoft.Graph.Models;
using MyWorkHub.Infrastructure.Features.Teams;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Teams;

public sealed class GraphTeamsServiceTests
{
    private static readonly DateTimeOffset _sent = new(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);

    private static Chat ChatWith(string? topic, string sender = "Bob", DateTimeOffset? lastReadAt = null) => new()
    {
        Id = "chat-1",
        Topic = topic,
        WebUrl = "https://teams.microsoft.com/l/chat/chat-1",
        LastMessagePreview = new ChatMessageInfo
        {
            CreatedDateTime = _sent,
            Body = new ItemBody { Content = "<p>Hi&nbsp;<b>there</b></p>\n<p>see you</p>" },
            From = new ChatMessageFromIdentitySet { User = new Identity { DisplayName = sender } },
        },
        Viewpoint = lastReadAt is null ? null : new ChatViewpoint { LastMessageReadDateTime = lastReadAt },
    };

    [Fact]
    public async Task Should_request_recent_chats_with_their_last_message_newest_first()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => new ChatCollectionResponse { Value = [ChatWith("Team")] });

        var chats = await new GraphTeamsService(adapter.Client).GetRecentChatsAsync(TestContext.Current.CancellationToken);

        Assert.Single(chats);
        var request = Assert.Single(adapter.Requests);
        Assert.Equal("/me/chats", request.GraphPath());
        var query = request.DecodedQuery();
        Assert.Contains("$expand=lastMessagePreview", query, StringComparison.Ordinal);
        Assert.Contains("$orderby=lastMessagePreview/createdDateTime desc", query, StringComparison.Ordinal);
        Assert.Contains("$top=50", query, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_map_a_titled_chat_with_a_plain_text_preview()
    {
        var item = GraphTeamsMapper.ToTeamsChatItem(ChatWith("Release crew"));

        Assert.Equal("Release crew", item.Title);
        Assert.Equal("Hi there see you", item.LastMessagePreview);
        Assert.Equal("Bob", item.LastSenderName);
        Assert.Equal(_sent.UtcDateTime, item.LastMessageAt);
        Assert.Equal("https://teams.microsoft.com/l/chat/chat-1", item.WebUrl);
    }

    [Fact]
    public void Should_title_an_untitled_chat_after_the_latest_sender()
    {
        Assert.Equal("Bob", GraphTeamsMapper.ToTeamsChatItem(ChatWith(null)).Title);
        Assert.Equal("Chat", GraphTeamsMapper.ToTeamsChatItem(ChatWith(" ", sender: "")).Title);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    public void Should_flag_the_chat_unread_when_its_last_message_is_newer_than_the_last_read(int? readOffsetMinutes, bool expected)
    {
        var chat = ChatWith("Team", lastReadAt: readOffsetMinutes is { } offset ? _sent.AddMinutes(offset) : null);

        Assert.Equal(expected, GraphTeamsMapper.ToTeamsChatItem(chat).IsUnread);
    }

    [Fact]
    public void Should_not_flag_an_empty_chat_unread()
    {
        Assert.False(GraphTeamsMapper.ToTeamsChatItem(new Chat { Id = "empty" }).IsUnread);
    }
}
