using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using MyWorkHub.Core.Features.Email;
using MyWorkHub.Core.Features.GraphAuth;
using MyWorkHub.Infrastructure.Configuration;
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
        => new(adapter.Client, Live.Of(new EmailOptions(folderIds, 25)), NullLogger<GraphEmailService>.Instance);

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

    // --- HTML body -----------------------------------------------------------------------

    [Fact]
    public async Task Should_return_an_html_body_as_is_without_asking_for_text()
    {
        using var adapter = new FakeGraphRequestAdapter(
            _ => new Message { Body = new ItemBody { ContentType = BodyType.Html, Content = "<p>Hi</p>" } });

        var html = await Service(adapter, "inbox").GetEmailHtmlAsync("msg-1", TestContext.Current.CancellationToken);

        Assert.Equal("<p>Hi</p>", html);
        var request = Assert.Single(adapter.Requests);
        Assert.False(request.Headers.ContainsKey("Prefer"));
    }

    [Fact]
    public async Task Should_escape_a_plain_text_body_into_a_preformatted_block()
    {
        using var adapter = new FakeGraphRequestAdapter(
            _ => new Message { Body = new ItemBody { ContentType = BodyType.Text, Content = "a <b> & c" } });

        var html = await Service(adapter, "inbox").GetEmailHtmlAsync("msg-1", TestContext.Current.CancellationToken);

        Assert.Contains("a &lt;b&gt; &amp; c", html, StringComparison.Ordinal);
        Assert.StartsWith("<pre", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_embed_inline_images_as_data_uris_when_the_body_references_cid()
    {
        using var adapter = new FakeGraphRequestAdapter(request => request.GraphPath().EndsWith("/attachments", StringComparison.Ordinal)
            ? new AttachmentCollectionResponse
            {
                Value =
                [
                    new FileAttachment { IsInline = true, ContentId = "logo@1", ContentType = "image/png", ContentBytes = [1, 2, 3] },
                    new FileAttachment { IsInline = true, ContentId = "doc@1", ContentType = "text/html", ContentBytes = [4] },
                ],
            }
            : new Message { Body = new ItemBody { ContentType = BodyType.Html, Content = "<img src=\"cid:logo@1\"><img src=\"cid:doc@1\">" } });

        var html = await Service(adapter, "inbox").GetEmailHtmlAsync("msg-1", TestContext.Current.CancellationToken);

        Assert.Equal("<img src=\"data:image/png;base64,AQID\"><img src=\"cid:doc@1\">", html);
        Assert.Equal("/me/messages/msg-1/attachments", adapter.Requests[1].GraphPath());
    }

    // --- Folders -------------------------------------------------------------------------

    [Fact]
    public async Task Should_build_the_folder_tree_by_fetching_children_of_folders_that_have_some()
    {
        using var adapter = new FakeGraphRequestAdapter(request => request.GraphPath() switch
        {
            "/me/mailFolders" => new MailFolderCollectionResponse
            {
                Value =
                [
                    new MailFolder { Id = "inbox-id", DisplayName = "Inbox", UnreadItemCount = 3, ChildFolderCount = 1 },
                    new MailFolder { Id = "sent-id", DisplayName = "Sent", ChildFolderCount = 0 },
                ],
            },
            "/me/mailFolders/inbox-id/childFolders" => new MailFolderCollectionResponse
            {
                Value = [new MailFolder { Id = "azdo-id", DisplayName = "Azure DevOps", UnreadItemCount = 7 }],
            },
            _ => throw new ODataError { ResponseStatusCode = 404 },
        });

        var folders = await Service(adapter, "inbox").GetFoldersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Inbox", "Sent"], folders.Select(f => f.DisplayName));
        Assert.Equal(3, folders[0].UnreadCount);
        var child = Assert.Single(folders[0].Children);
        Assert.Equal(("azdo-id", "Azure DevOps", 7), (child.Id, child.DisplayName, child.UnreadCount));
        Assert.Empty(folders[1].Children);
    }

    [Fact]
    public async Task Should_put_the_standard_folders_first_in_outlook_order_then_the_others_alphabetically()
    {
        using var adapter = new FakeGraphRequestAdapter(request => request.GraphPath() switch
        {
            "/me/mailFolders" => new MailFolderCollectionResponse
            {
                Value =
                [
                    new MailFolder { Id = "archive-id", DisplayName = "Archive" },
                    new MailFolder { Id = "zeta-id", DisplayName = "Zeta" },
                    new MailFolder { Id = "deleted-id", DisplayName = "Deleted Items" },
                    new MailFolder { Id = "alpha-id", DisplayName = "Alpha" },
                    new MailFolder { Id = "sent-id", DisplayName = "Sent Items" },
                    new MailFolder { Id = "inbox-id", DisplayName = "Inbox" },
                ],
            },
            "/me/mailFolders/inbox" => new MailFolder { Id = "inbox-id" },
            "/me/mailFolders/sentitems" => new MailFolder { Id = "sent-id" },
            "/me/mailFolders/deleteditems" => new MailFolder { Id = "deleted-id" },
            "/me/mailFolders/archive" => new MailFolder { Id = "archive-id" },
            _ => throw new ODataError { ResponseStatusCode = 404 },
        });

        var folders = await Service(adapter, "inbox").GetFoldersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Inbox", "Sent Items", "Deleted Items", "Archive", "Alpha", "Zeta"], folders.Select(f => f.DisplayName));
    }

    [Fact]
    public async Task Should_list_one_folder_newest_first_with_its_name()
    {
        using var adapter = Mailbox(new()
        {
            ["azdo-id"] = ("Azure DevOps", [MessageAt("a", _monday), MessageAt("b", _monday.AddHours(1))]),
        });

        var emails = await Service(adapter, "inbox").GetFolderEmailsAsync("azdo-id", TestContext.Current.CancellationToken);

        Assert.Equal(["b", "a"], emails.Select(e => e.Id));
        Assert.All(emails, e => Assert.Equal("Azure DevOps", e.FolderName));
    }

    [Fact]
    public async Task Should_resolve_a_well_known_folder_name_to_its_real_id()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => new MailFolder { Id = "AAMk-inbox" });

        var id = await Service(adapter, "inbox").GetFolderIdAsync("inbox", TestContext.Current.CancellationToken);

        Assert.Equal("AAMk-inbox", id);
        Assert.Equal("/me/mailFolders/inbox", Assert.Single(adapter.Requests).GraphPath());
    }

    [Fact]
    public async Task Should_return_null_for_a_folder_that_no_longer_exists()
    {
        using var adapter = new FakeGraphRequestAdapter(_ => throw new ODataError { ResponseStatusCode = 404 });

        Assert.Null(await Service(adapter, "inbox").GetFolderIdAsync("gone", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_use_the_current_watched_folders_on_every_load()
    {
        using var adapter = Mailbox(new()
        {
            ["inbox"] = ("Inbox", [MessageAt("a", _monday)]),
            ["azdo-id"] = ("Azure DevOps", [MessageAt("b", _monday)]),
        });
        var options = new EmailOptions(["inbox"], 25);
        var service = new GraphEmailService(adapter.Client, new LiveOptions<EmailOptions>(() => options), NullLogger<GraphEmailService>.Instance);
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(["a"], (await service.GetRecentEmailsAsync(ct)).Select(e => e.Id));

        options = new EmailOptions(["azdo-id"], 25);

        Assert.Equal(["b"], (await service.GetRecentEmailsAsync(ct)).Select(e => e.Id));
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

        Assert.Equal("It's here & ready now", email.Preview);
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
