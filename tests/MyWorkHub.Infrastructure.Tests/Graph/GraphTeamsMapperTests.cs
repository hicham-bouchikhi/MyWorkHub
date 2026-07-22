using MyWorkHub.Infrastructure.Graph;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class GraphTeamsMapperTests
{
    [Fact]
    public void Is_unread_when_last_message_is_newer_than_last_read()
    {
        var chat = new Chat
        {
            LastMessagePreview = new ChatMessageInfo { CreatedDateTime = At("09:00") },
            Viewpoint = new ChatViewpoint { LastMessageReadDateTime = At("08:00") },
        };

        Assert.True(GraphTeamsMapper.IsUnread(chat));
    }

    [Fact]
    public void Is_unread_when_chat_has_a_message_but_was_never_read()
    {
        var chat = new Chat
        {
            LastMessagePreview = new ChatMessageInfo { CreatedDateTime = At("09:00") },
            Viewpoint = null,
        };

        Assert.True(GraphTeamsMapper.IsUnread(chat));
    }

    [Fact]
    public void Is_read_when_last_read_is_at_or_after_the_last_message()
    {
        var chat = new Chat
        {
            LastMessagePreview = new ChatMessageInfo { CreatedDateTime = At("09:00") },
            Viewpoint = new ChatViewpoint { LastMessageReadDateTime = At("09:00") },
        };

        Assert.False(GraphTeamsMapper.IsUnread(chat));
    }

    [Fact]
    public void Is_not_unread_when_chat_has_no_messages()
    {
        var chat = new Chat { LastMessagePreview = null };

        Assert.False(GraphTeamsMapper.IsUnread(chat));
    }

    [Fact]
    public void Maps_all_fields_from_a_group_chat_with_a_topic()
    {
        var chat = new Chat
        {
            Id = "19:chat-1",
            Topic = "Release planning",
            WebUrl = "https://teams.microsoft.com/l/chat/19:chat-1",
            LastMessagePreview = new ChatMessageInfo
            {
                CreatedDateTime = At("09:30"),
                From = new ChatMessageFromIdentitySet { User = new Identity { DisplayName = "Alice Martin" } },
                Body = new ItemBody { Content = "Can we move the demo?" },
            },
        };

        var item = GraphTeamsMapper.ToTeamsChatItem(chat);

        Assert.Equal("19:chat-1", item.ChatId);
        Assert.Equal("Release planning", item.ChatName);
        Assert.Equal("Alice Martin", item.LastSenderName);
        Assert.Equal("Can we move the demo?", item.MessagePreview);
        Assert.Equal(At("09:30").UtcDateTime, item.ReceivedAt);
        Assert.Equal("https://teams.microsoft.com/l/chat/19:chat-1", item.DeepLinkUrl);
        Assert.Equal(1, item.UnreadCount);
    }

    [Fact]
    public void Names_a_topicless_chat_after_the_last_sender()
    {
        var chat = new Chat
        {
            Id = "19:dm",
            LastMessagePreview = new ChatMessageInfo
            {
                CreatedDateTime = At("10:00"),
                From = new ChatMessageFromIdentitySet { User = new Identity { DisplayName = "Bob Durand" } },
                Body = new ItemBody { Content = "Ping" },
            },
        };

        var item = GraphTeamsMapper.ToTeamsChatItem(chat);

        Assert.Equal("Bob Durand", item.ChatName);
    }

    [Fact]
    public void Uses_placeholders_for_missing_sender_topic_and_body()
    {
        var chat = new Chat
        {
            Id = "19:empty",
            LastMessagePreview = new ChatMessageInfo { CreatedDateTime = At("10:00") },
        };

        var item = GraphTeamsMapper.ToTeamsChatItem(chat);

        Assert.Equal("(unknown sender)", item.LastSenderName);
        Assert.Equal("(group chat)", item.ChatName);
        Assert.Equal("", item.MessagePreview);
        Assert.Equal("", item.DeepLinkUrl);
    }

    [Fact]
    public void Strips_html_tags_from_an_html_message_preview()
    {
        var item = GraphTeamsMapper.ToTeamsChatItem(HtmlPreview("<p>Hello <b>world</b></p>"));

        Assert.Equal("Hello world", item.MessagePreview);
    }

    [Fact]
    public void Decodes_html_entities_in_the_preview()
    {
        var item = GraphTeamsMapper.ToTeamsChatItem(HtmlPreview("<div>R&amp;D meeting&nbsp;tomorrow</div>"));

        Assert.Equal("R&D meeting tomorrow", item.MessagePreview);
    }

    [Fact]
    public void Keeps_mention_text_and_turns_line_breaks_into_spaces()
    {
        var item = GraphTeamsMapper.ToTeamsChatItem(HtmlPreview("Hi <at id=\"0\">Alice</at>,<br>are you there?"));

        Assert.Equal("Hi Alice, are you there?", item.MessagePreview);
    }

    [Fact]
    public void Leaves_a_plain_text_preview_untouched()
    {
        var chat = new Chat
        {
            Id = "19:text",
            LastMessagePreview = new ChatMessageInfo
            {
                CreatedDateTime = At("10:00"),
                Body = new ItemBody { ContentType = BodyType.Text, Content = "Plain & simple <not a tag>" },
            },
        };

        var item = GraphTeamsMapper.ToTeamsChatItem(chat);

        Assert.Equal("Plain & simple <not a tag>", item.MessagePreview);
    }

    private static Chat HtmlPreview(string html) => new()
    {
        Id = "19:html",
        LastMessagePreview = new ChatMessageInfo
        {
            CreatedDateTime = At("10:00"),
            Body = new ItemBody { ContentType = BodyType.Html, Content = html },
        },
    };

    private static DateTimeOffset At(string time) =>
        DateTimeOffset.Parse($"2026-06-22T{time}:00Z", System.Globalization.CultureInfo.InvariantCulture);
}
