using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Infrastructure.Features.Email;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.Email;

public sealed class GraphEmailServiceTests
{
    private static readonly DateTimeOffset _monday = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);

    private static Message MessageAt(string id, DateTimeOffset received, string subject = "Subject") => new()
    {
        Id = id,
        Subject = subject,
        BodyPreview = "Preview",
        ReceivedDateTime = received,
        IsRead = false,
        From = new Recipient { EmailAddress = new EmailAddress { Name = "Alice", Address = "alice@cegid.com" } },
    };

    private static GraphEmailService Service(FakeGraphRequestAdapter adapter, params string[] folderIds)
        => new(adapter.Client, new EmailOptions(folderIds, 25), NullLogger<GraphEmailService>.Instance);

    /// <summary>Answers folder lookups with a display name and message listings with the given messages.</summary>
    private static FakeGraphRequestAdapter Mailbox(Dictionary<string, (string Name, Message[] Messages)> folders)
        => new(request =>
        {
            var segments = request.GraphPath().Split('/', StringSplitOptions.RemoveEmptyEntries);
            // me / mailFolders / {id} [/ messages]
            if (!folders.TryGetValue(segments[2], out var folder))
            {
                throw new ODataError { ResponseStatusCode = 404 };
            }

            return segments.Length == 3
                ? new MailFolder { DisplayName = folder.Name }
                : new MessageCollectionResponse { Value = [.. folder.Messages] };
        });

    // --- List ----------------------------------------------------------------------------

    [Fact]
    public async Task Should_merge_the_watched_folders_newest_first_with_their_folder_names()
    {
        using var adapter = Mailbox(new()
        {
            ["inbox"] = ("Boîte de réception", [MessageAt("a", _monday), MessageAt("c", _monday.AddHours(2))]),
            ["archive-id"] = ("Archive", [MessageAt("b", _monday.AddHours(1))]),
        });

        var emails = await Service(adapter, "inbox", "archive-id").GetRecentEmailsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["c", "b", "a"], emails.Select(e => e.Id));
        Assert.Equal(["Boîte de réception", "Archive", "Boîte de réception"], emails.Select(e => e.FolderName));
    }

    [Fact]
    public async Task Should_request_the_newest_messages_with_only_the_listed_fields()
    {
        using var adapter = Mailbox(new() { ["inbox"] = ("Inbox", []) });

        await Service(adapter, "inbox").GetRecentEmailsAsync(TestContext.Current.CancellationToken);

        var listing = Assert.Single(adapter.Requests, r => r.GraphPath() == "/me/mailFolders/inbox/messages");
        var query = listing.DecodedQuery();
        Assert.Contains("$top=25", query, StringComparison.Ordinal);
        Assert.Contains("$orderby=receivedDateTime desc", query, StringComparison.Ordinal);
        Assert.Contains("$select=id,from,subject,bodyPreview,receivedDateTime,flag,isRead", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_skip_a_watched_folder_that_no_longer_exists()
    {
        using var adapter = Mailbox(new() { ["inbox"] = ("Inbox", [MessageAt("a", _monday)]) });

        var emails = await Service(adapter, "deleted-folder", "inbox").GetRecentEmailsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["a"], emails.Select(e => e.Id));
    }

    [Fact]
    public async Task Should_list_a_message_once_when_its_folder_is_watched_under_two_ids()
    {
        var shared = MessageAt("a", _monday);
        using var adapter = Mailbox(new() { ["inbox"] = ("Inbox", [shared]), ["AAMkInbox"] = ("Inbox", [shared]) });

        var emails = await Service(adapter, "inbox", "AAMkInbox").GetRecentEmailsAsync(TestContext.Current.CancellationToken);

        Assert.Single(emails);
    }

    [Fact]
    public async Task Should_let_other_graph_errors_surface()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => throw new ODataError { ResponseStatusCode = 500 });

        await Assert.ThrowsAsync<ODataError>(() => Service(adapter, "inbox").GetRecentEmailsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_let_the_not_connected_signal_surface_so_the_page_can_offer_sign_in()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => throw new GraphNotConnectedException());

        await Assert.ThrowsAsync<GraphNotConnectedException>(() => Service(adapter, "inbox").GetRecentEmailsAsync(TestContext.Current.CancellationToken));
    }

    // --- Body ----------------------------------------------------------------------------

    [Fact]
    public async Task Should_fetch_the_body_as_plain_text()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => new Message { Body = new ItemBody { Content = "Hello" } });

        var body = await Service(adapter, "inbox").GetEmailBodyAsync("msg-1", TestContext.Current.CancellationToken);

        Assert.Equal("Hello", body);
        var request = Assert.Single(adapter.Requests);
        Assert.Equal("/me/messages/msg-1", request.GraphPath());
        Assert.Contains("outlook.body-content-type=\"text\"", request.Headers["Prefer"]);
    }

    [Fact]
    public async Task Should_decode_html_entities_left_over_from_the_text_conversion()
    {
        using var adapter = new FakeGraphRequestAdapter(
            _ => new Message { Body = new ItemBody { Content = "Caf&eacute;&nbsp;&amp; croissants &#39;today&#39;" } });

        var body = await Service(adapter, "inbox").GetEmailBodyAsync("msg-1", TestContext.Current.CancellationToken);

        Assert.Equal("Café & croissants 'today'", body);
    }

    // --- Mapping -------------------------------------------------------------------------

    [Fact]
    public void Should_map_the_header_fields_of_a_message()
    {
        var message = MessageAt("id-1", _monday, "Weekly sync");
        message.Flag = new FollowupFlag { FlagStatus = FollowupFlagStatus.Flagged };
        message.IsRead = true;

        var email = GraphEmailMapper.ToEmailItem(message, "Inbox");

        Assert.Equal(
            new EmailItem("id-1", "Alice", "Weekly sync", "Preview", _monday.UtcDateTime, true, true, "Inbox"),
            email);
        Assert.Equal(DateTimeKind.Utc, email.ReceivedAt.Kind);
    }

    [Fact]
    public void Should_decode_html_entities_in_the_preview()
    {
        var message = MessageAt("id-1", _monday);
        message.BodyPreview = "It&#39;s here &amp; ready&nbsp;now";

        var email = GraphEmailMapper.ToEmailItem(message, "Inbox");

        Assert.Equal("It's here & ready now", email.Preview);
    }

    [Fact]
    public void Should_fall_back_to_the_sender_address_and_empty_values_when_fields_are_missing()
    {
        var message = new Message { From = new Recipient { EmailAddress = new EmailAddress { Address = "bot@cegid.com" } } };

        var email = GraphEmailMapper.ToEmailItem(message, "Inbox");

        Assert.Equal("bot@cegid.com", email.From);
        Assert.Equal("", email.Subject);
        Assert.False(email.IsFlagged);
    }

    // --- Options -------------------------------------------------------------------------

    [Fact]
    public void Should_read_the_watched_folders_and_clamp_the_page_size()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:FolderIds:0"] = "inbox",
            ["Email:FolderIds:1"] = "INBOX",
            ["Email:FolderIds:2"] = " archive ",
            ["Email:MaxPerFolder"] = "5000",
        }).Build();

        var options = EmailOptions.FromConfiguration(configuration);

        Assert.Equal(["inbox", "archive"], options.FolderIds);
        Assert.Equal(100, options.MaxPerFolder);
    }

    [Fact]
    public void Should_watch_the_inbox_by_default()
    {
        var options = EmailOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.Equal(["inbox"], options.FolderIds);
        Assert.Equal(25, options.MaxPerFolder);
    }
}
