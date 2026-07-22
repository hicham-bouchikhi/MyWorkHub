using MyWorkHub.Infrastructure.Graph;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class GraphEmailMapperTests
{
    [Fact]
    public void Maps_all_fields_from_a_fully_populated_message()
    {
        var received = new DateTimeOffset(2026, 6, 20, 8, 30, 0, TimeSpan.Zero);
        var message = new Message
        {
            Id = "AAMk-123",
            Subject = "Quarterly review",
            BodyPreview = "Please read before Friday",
            ReceivedDateTime = received,
            From = new Recipient { EmailAddress = new EmailAddress { Name = "Alice Martin", Address = "alice@cegid.com" } },
            Flag = new FollowupFlag { FlagStatus = FollowupFlagStatus.Flagged },
        };

        var item = GraphEmailMapper.ToEmailItem(message);

        Assert.Equal("AAMk-123", item.Id);
        Assert.Equal("Alice Martin", item.From);
        Assert.Equal("Quarterly review", item.Subject);
        Assert.Equal("Please read before Friday", item.Preview);
        Assert.Equal(received.UtcDateTime, item.ReceivedAt);
        Assert.True(item.IsFlagged);
    }

    [Fact]
    public void Falls_back_to_email_address_when_display_name_is_missing()
    {
        var message = new Message
        {
            From = new Recipient { EmailAddress = new EmailAddress { Address = "bob@cegid.com" } },
        };

        var item = GraphEmailMapper.ToEmailItem(message);

        Assert.Equal("bob@cegid.com", item.From);
    }

    [Fact]
    public void Uses_placeholders_for_missing_sender_and_subject()
    {
        var item = GraphEmailMapper.ToEmailItem(new Message());

        Assert.Equal("(unknown sender)", item.From);
        Assert.Equal("(no subject)", item.Subject);
        Assert.False(item.IsFlagged);
    }

    [Fact]
    public void Is_not_flagged_when_flag_status_is_not_flagged()
    {
        var message = new Message { Flag = new FollowupFlag { FlagStatus = FollowupFlagStatus.Complete } };

        var item = GraphEmailMapper.ToEmailItem(message);

        Assert.False(item.IsFlagged);
    }

    [Fact]
    public void Maps_folder_name_when_provided()
    {
        var item = GraphEmailMapper.ToEmailItem(new Message { Id = "x" }, "Drafts");

        Assert.Equal("Drafts", item.FolderName);
    }

    [Fact]
    public void Folder_name_defaults_to_empty_when_not_provided()
    {
        var item = GraphEmailMapper.ToEmailItem(new Message { Id = "x" });

        Assert.Equal("", item.FolderName);
    }
}
