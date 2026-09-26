using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using MyWorkHub.Core.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Tests.TestDoubles;
using static MyWorkHub.Infrastructure.Tests.TestDoubles.StubHttpMessageHandler;

namespace MyWorkHub.Infrastructure.Tests.Features.AzureDevOps;

public sealed class AzureDevOpsServiceTests
{
    private const string ME_ID = AzureDevOpsFixture.ME_ID;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Path(HttpRequestMessage request) => request.RequestUri!.AbsolutePath;

    private static string Query(HttpRequestMessage request) => Uri.UnescapeDataString(request.RequestUri!.Query);

    private static string PullRequestJson(
        int id, string created, string repository = "web", string project = "Alpha", string title = "Title",
        string reviewers = "[]", bool isDraft = false)
        => $$$"""
           {"pullRequestId":{{{id}}},"title":"{{{title}}}","createdBy":{"id":"someone","displayName":"Alice"},
            "repository":{"name":"{{{repository}}}","project":{"name":"{{{project}}}"}},
            "creationDate":"{{{created}}}","reviewers":{{{reviewers}}},
            "sourceRefName":"refs/heads/feature/login","targetRefName":"refs/heads/main","isDraft":{{{(isDraft ? "true" : "false")}}}}
           """;

    private static HttpResponseMessage PullRequests(params string[] pullRequests)
        => Json($$"""{"value":[{{string.Join(',', pullRequests)}}],"count":{{pullRequests.Length}}}""");

    /// <summary>Routes the per-project creator / reviewer searches.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage?> PullRequestSearch(
        Dictionary<string, string[]> authored, Dictionary<string, string[]> reviewing)
        => request =>
        {
            var segments = Path(request).Split('/', StringSplitOptions.RemoveEmptyEntries); // cegid/{project}/_apis/git/pullrequests
            var project = Uri.UnescapeDataString(segments[1]);
            var table = Query(request).Contains("creatorId", StringComparison.Ordinal) ? authored : reviewing;
            return PullRequests(table.GetValueOrDefault(project) ?? []);
        };

    // --- Pull requests -------------------------------------------------------------------------

    [Fact]
    public async Task Should_list_authored_and_reviewed_pull_requests_of_every_project_newest_first()
    {
        using var fixture = new AzureDevOpsFixture(
            PullRequestSearch(
                authored: new() { ["Alpha"] = [PullRequestJson(1, "2026-09-20T10:00:00Z")] },
                reviewing: new() { ["Beta"] = [PullRequestJson(2, "2026-09-22T10:00:00Z", project: "Beta")] }),
            projects: ["Alpha", "Beta"]);

        var pullRequests = await fixture.Service.GetMyPullRequestsAsync(Ct);

        Assert.Equal([2, 1], pullRequests.Select(p => p.Id));
        Assert.Equal([PullRequestRole.REVIEWER, PullRequestRole.AUTHOR], pullRequests.Select(p => p.Role));
    }

    [Fact]
    public async Task Should_map_the_fields_of_a_pull_request()
    {
        using var fixture = new AzureDevOpsFixture(PullRequestSearch(
            authored: [],
            reviewing: new()
            {
                ["Alpha"] = [PullRequestJson(42, "2026-09-21T08:30:00+02:00", repository: "my repo", project: "Alpha Team", title: "Add login",
                    reviewers: $$"""[{"id":"other","vote":-10},{"id":"{{ME_ID}}","vote":5}]""", isDraft: true)],
            }));

        var pr = Assert.Single(await fixture.Service.GetMyPullRequestsAsync(Ct));

        Assert.Equal(
            new PullRequestItem(42, "Add login", "my repo", "Alpha Team", "Alice", "feature/login", "main",
                new DateTime(2026, 9, 21, 6, 30, 0, DateTimeKind.Utc), PullRequestRole.REVIEWER, PullRequestVote.SUGGESTIONS,
                IsDraft: true, "https://dev.azure.com/cegid/Alpha%20Team/_git/my%20repo/pullrequest/42"),
            pr);
        Assert.Equal(DateTimeKind.Utc, pr.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(10, PullRequestVote.APPROVED)]
    [InlineData(5, PullRequestVote.SUGGESTIONS)]
    [InlineData(0, PullRequestVote.NONE)]
    [InlineData(-5, PullRequestVote.WAITING)]
    [InlineData(-10, PullRequestVote.REJECTED)]
    public async Task Should_map_the_users_own_reviewer_vote(int vote, PullRequestVote expected)
    {
        using var fixture = new AzureDevOpsFixture(PullRequestSearch(
            authored: [],
            reviewing: new() { ["Alpha"] = [PullRequestJson(1, "2026-09-21T00:00:00Z", reviewers: $$"""[{"id":"{{ME_ID}}","vote":{{vote}}}]""")] }));

        var pr = Assert.Single(await fixture.Service.GetMyPullRequestsAsync(Ct));

        Assert.Equal(expected, pr.MyVote);
    }

    [Fact]
    public async Task Should_list_a_pull_request_once_as_authored_when_the_user_also_reviews_it()
    {
        var both = PullRequestJson(7, "2026-09-21T00:00:00Z");
        using var fixture = new AzureDevOpsFixture(PullRequestSearch(
            authored: new() { ["Alpha"] = [both] },
            reviewing: new() { ["Alpha"] = [both] }));

        var pr = Assert.Single(await fixture.Service.GetMyPullRequestsAsync(Ct));

        Assert.Equal(PullRequestRole.AUTHOR, pr.Role);
        Assert.Equal(PullRequestVote.NONE, pr.MyVote);
    }

    [Fact]
    public async Task Should_search_active_pull_requests_by_the_current_user_per_project()
    {
        using var fixture = new AzureDevOpsFixture(PullRequestSearch([], []), projects: ["My Project"]);

        await fixture.Service.GetMyPullRequestsAsync(Ct);

        var searches = fixture.DataRequests.ToList();
        Assert.Equal(2, searches.Count);
        Assert.All(searches, r =>
        {
            Assert.Equal(HttpMethod.Get, r.Method);
            Assert.Equal("/cegid/My%20Project/_apis/git/pullrequests", r.Uri.AbsolutePath);
            Assert.Contains("searchCriteria.status=active", r.DecodedQuery, StringComparison.Ordinal);
            Assert.Contains("api-version=7.1", r.DecodedQuery, StringComparison.Ordinal);
        });
        Assert.Contains(searches, r => r.DecodedQuery.Contains($"searchCriteria.creatorId={ME_ID}", StringComparison.Ordinal));
        Assert.Contains(searches, r => r.DecodedQuery.Contains($"searchCriteria.reviewerId={ME_ID}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_authenticate_every_request_with_the_stored_token_as_basic_auth()
    {
        using var fixture = new AzureDevOpsFixture(PullRequestSearch([], []));

        await fixture.Service.GetMyPullRequestsAsync(Ct);

        var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + AzureDevOpsFixture.PAT));
        Assert.All(fixture.Handler.Requests, r => Assert.Equal(expected, r.Authorization));
        Assert.All(fixture.Factory.RequestedNames, n => Assert.Equal(AzureDevOpsClient.HTTP_CLIENT_NAME, n));
    }

    [Fact]
    public async Task Should_report_not_connected_without_sending_anything_when_no_token_is_stored()
    {
        using var fixture = new AzureDevOpsFixture(storedToken: null);

        await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => fixture.Service.GetMyPullRequestsAsync(Ct));
        await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => fixture.Service.GetMyWorkItemsAsync(Ct));
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task Should_report_not_connected_when_the_organization_url_is_missing()
    {
        using var fixture = new AzureDevOpsFixture(organizationUrl: null);

        var ex = await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => fixture.Service.GetMyWorkItemsAsync(Ct));

        Assert.Contains("OrganizationUrl", ex.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)] // the HTML sign-in page some endpoints serve
    public async Task Should_report_not_connected_when_azure_devops_rejects_the_token(HttpStatusCode status)
    {
        using var fixture = new AzureDevOpsFixture(_ => Status(status, "<html>Sign in</html>"));

        await Assert.ThrowsAsync<AzureDevOpsNotConnectedException>(() => fixture.Service.GetMyWorkItemsAsync(Ct));
    }

    [Fact]
    public async Task Should_explain_that_projects_must_be_configured_when_none_are()
    {
        using var fixture = new AzureDevOpsFixture(projects: []);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.GetMyPullRequestsAsync(Ct));

        Assert.Contains("AzureDevOps:Projects", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_surface_other_failures_with_the_status_code_and_server_message()
    {
        using var fixture = new AzureDevOpsFixture(_ => Status(HttpStatusCode.NotFound, "TF200016: project 'Alpha' does not exist"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.GetMyPullRequestsAsync(Ct));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Contains("TF200016", ex.Message, StringComparison.Ordinal);
    }

    // --- Work items ----------------------------------------------------------------------------

    private static string WorkItemJson(int id, string fields)
        => "{\"id\":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"fields\":{" + fields + "}}";

    /// <summary>Answers the WIQL query with <paramref name="ids"/> and the batch fetch with <paramref name="items"/>.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage?> WorkItemSearch(int[] ids, params string[] items)
        => request => Path(request) switch
        {
            "/cegid/_apis/wit/wiql" => Json($$"""{"workItems":[{{string.Join(',', ids.Select(i => $$"""{"id":{{i}}}"""))}}]}"""),
            "/cegid/_apis/wit/workitems" => Json($$"""{"value":[{{string.Join(',', items)}}]}"""),
            _ => null,
        };

    [Fact]
    public async Task Should_query_open_items_assigned_to_me_across_the_organization()
    {
        using var fixture = new AzureDevOpsFixture(WorkItemSearch([]));

        await fixture.Service.GetMyWorkItemsAsync(Ct);

        var wiql = Assert.Single(fixture.DataRequests);
        Assert.Equal(HttpMethod.Post, wiql.Method);
        Assert.Equal("/cegid/_apis/wit/wiql", wiql.Uri.AbsolutePath);
        Assert.Contains("api-version=7.1", wiql.DecodedQuery, StringComparison.Ordinal);
        using var body = System.Text.Json.JsonDocument.Parse(wiql.Body!);
        var query = body.RootElement.GetProperty("query").GetString();
        Assert.Contains("[System.AssignedTo] = @Me", query, StringComparison.Ordinal);
        Assert.Contains("NOT IN ('Closed', 'Removed', 'Done')", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_map_the_fields_of_a_work_item()
    {
        using var fixture = new AzureDevOpsFixture(WorkItemSearch([12],
            WorkItemJson(12, """
                "System.Title":"Fix login","System.WorkItemType":"Bug","System.State":"Active",
                "System.TeamProject":"Alpha Team","Microsoft.VSTS.Common.Priority":2,
                "Microsoft.VSTS.Scheduling.DueDate":"2026-10-03T00:00:00Z","Microsoft.VSTS.Scheduling.StoryPoints":3.5
                """)));

        var item = Assert.Single(await fixture.Service.GetMyWorkItemsAsync(Ct));

        Assert.Equal(
            new WorkItem(12, "Fix login", "Bug", "Active", "2", new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                "https://dev.azure.com/cegid/Alpha%20Team/_workitems/edit/12", "3.5", "Alpha Team"),
            item);
    }

    [Fact]
    public async Task Should_fall_back_to_the_effort_field_and_empty_values_for_missing_fields()
    {
        using var fixture = new AzureDevOpsFixture(WorkItemSearch([5],
            WorkItemJson(5, """ "System.Title":"Backlog item","System.TeamProject":"Alpha","Microsoft.VSTS.Scheduling.Effort":8 """)));

        var item = Assert.Single(await fixture.Service.GetMyWorkItemsAsync(Ct));

        Assert.Equal("8", item.Effort);
        Assert.Equal("", item.Priority);
        Assert.Equal("", item.Type);
        Assert.Null(item.DueDate);
    }

    [Fact]
    public async Task Should_keep_the_query_order_most_recently_changed_first()
    {
        using var fixture = new AzureDevOpsFixture(WorkItemSearch([30, 10, 20],
            WorkItemJson(10, "\"System.TeamProject\":\"Alpha\""),
            WorkItemJson(20, "\"System.TeamProject\":\"Alpha\""),
            WorkItemJson(30, "\"System.TeamProject\":\"Alpha\"")));

        var items = await fixture.Service.GetMyWorkItemsAsync(Ct);

        Assert.Equal([30, 10, 20], items.Select(i => i.Id));
        var fetch = Assert.Single(fixture.DataRequests, r => r.Method == HttpMethod.Get);
        Assert.Contains("ids=30,10,20", fetch.DecodedQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_fetch_the_items_in_batches_of_two_hundred()
    {
        var ids = Enumerable.Range(1, 250).ToArray();
        using var fixture = new AzureDevOpsFixture(WorkItemSearch(ids));

        await fixture.Service.GetMyWorkItemsAsync(Ct);

        var fetches = fixture.DataRequests.Where(r => r.Method == HttpMethod.Get).ToList();
        Assert.Equal(2, fetches.Count);
        Assert.Contains("ids=1,2,", fetches[0].DecodedQuery, StringComparison.Ordinal);
        Assert.Contains(",200&", fetches[0].DecodedQuery, StringComparison.Ordinal);
        Assert.Contains("ids=201,", fetches[1].DecodedQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_not_fetch_any_item_when_nothing_is_assigned()
    {
        using var fixture = new AzureDevOpsFixture(WorkItemSearch([]));

        var items = await fixture.Service.GetMyWorkItemsAsync(Ct);

        Assert.Empty(items);
        Assert.DoesNotContain(fixture.DataRequests, r => r.Method == HttpMethod.Get);
    }

    // --- Mentions (preview API, best-effort) --------------------------------------------------------

    private static WorkItem Item(int id, string project = "Alpha")
        => new(id, "Item " + id, "Task", "Active", "", null, "https://dev.azure.com/cegid/Alpha/_workitems/edit/" + id, Project: project);

    private static string Mention(string name = AzureDevOpsFixture.ME_NAME)
        => $"""<div><a href=\"#\" data-vss-mention=\"version:2.0,{ME_ID}\">@{name}</a>&nbsp;can you check?</div>""";

    private static HttpResponseMessage Comments(params (int Id, string Text, string Created)[] comments)
        => Json($$"""
                {"totalCount":{{comments.Length}},"count":{{comments.Length}},"comments":[{{string.Join(',', comments.Select(c =>
                    $$"""{"id":{{c.Id}},"text":"{{c.Text}}","createdBy":{"displayName":"Bob"},"createdDate":"{{c.Created}}"}"""))}}]}
                """);

    /// <summary>Routes each work item's comments request to <paramref name="byWorkItem"/>.</summary>
    private static Func<HttpRequestMessage, HttpResponseMessage?> CommentsOf(Dictionary<int, Func<HttpResponseMessage>> byWorkItem)
        => request =>
        {
            var segments = Path(request).Split('/'); // "", cegid, {project}, _apis, wit, workItems, {id}, comments
            return byWorkItem.TryGetValue(int.Parse(segments[6], System.Globalization.CultureInfo.InvariantCulture), out var respond)
                ? respond()
                : null;
        };

    [Fact]
    public async Task Should_return_the_comments_mentioning_the_user_newest_first()
    {
        using var fixture = new AzureDevOpsFixture(CommentsOf(new()
        {
            [1] = () => Comments((101, Mention(), "2026-09-20T09:00:00Z"), (102, "<p>No mention here</p>", "2026-09-24T09:00:00Z")),
            [2] = () => Comments((201, Mention(), "2026-09-22T09:00:00Z")),
        }));

        var mentions = await fixture.Service.GetWorkItemMentionsAsync([Item(1), Item(2)], Ct);

        Assert.Equal([201, 101], mentions.Select(m => m.CommentId));
        Assert.Equal(
            new WorkItemMention(1, "Item 1", "https://dev.azure.com/cegid/Alpha/_workitems/edit/1", 101, "Bob",
                new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc), "@Jane Doe can you check?"),
            mentions[1]);
    }

    [Fact]
    public async Task Should_read_comments_per_project_through_the_preview_api_version()
    {
        using var fixture = new AzureDevOpsFixture(CommentsOf(new() { [7] = () => Comments() }));

        await fixture.Service.GetWorkItemMentionsAsync([Item(7, project: "Beta Team")], Ct);

        var request = Assert.Single(fixture.DataRequests);
        Assert.Equal("/cegid/Beta%20Team/_apis/wit/workItems/7/comments", request.Uri.AbsolutePath);
        Assert.Equal("?api-version=7.1-preview.3", request.DecodedQuery);
    }

    [Fact]
    public async Task Should_skip_a_work_item_whose_comments_cannot_be_read_and_keep_the_others()
    {
        // Best-effort: one preview-API failure (400, 404, network) must not hide the other items' mentions.
        using var fixture = new AzureDevOpsFixture(CommentsOf(new()
        {
            [1] = () => Status(HttpStatusCode.BadRequest, "VS403357: preview version deactivated"),
            [2] = () => Comments((201, Mention(), "2026-09-22T09:00:00Z")),
            [3] = () => throw new HttpRequestException("connection reset"),
            [4] = () => Status(HttpStatusCode.NotFound),
            [5] = () => Comments((501, Mention(), "2026-09-23T09:00:00Z")),
        }));

        var mentions = await fixture.Service.GetWorkItemMentionsAsync([Item(1), Item(2), Item(3), Item(4), Item(5)], Ct);

        Assert.Equal([501, 201], mentions.Select(m => m.CommentId));
        Assert.Equal(5, fixture.DataRequests.Count()); // every item was still tried
    }

    [Fact]
    public async Task Should_skip_a_work_item_whose_comments_are_malformed()
    {
        using var fixture = new AzureDevOpsFixture(CommentsOf(new()
        {
            [1] = () => Json("{ not json"),
            [2] = () => Comments((201, Mention(), "2026-09-22T09:00:00Z")),
        }));

        var mentions = await fixture.Service.GetWorkItemMentionsAsync([Item(1), Item(2)], Ct);

        Assert.Equal([201], mentions.Select(m => m.CommentId));
    }

    [Fact]
    public async Task Should_not_swallow_a_cancellation_while_reading_comments()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var fixture = new AzureDevOpsFixture(CommentsOf(new()
        {
            [1] = () =>
            {
                cancellation.Cancel();
                throw new TaskCanceledException();
            },
            [2] = () => Comments((201, Mention(), "2026-09-22T09:00:00Z")),
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Service.GetWorkItemMentionsAsync([Item(1), Item(2)], cancellation.Token));
    }

    [Fact]
    public async Task Should_ignore_mentions_of_other_people()
    {
        using var fixture = new AzureDevOpsFixture(CommentsOf(new()
        {
            [1] = () => Comments((101, Mention("Jane"), "2026-09-20T09:00:00Z"), (102, Mention("Jane Doerty"), "2026-09-20T09:00:00Z")),
        }));

        Assert.Empty(await fixture.Service.GetWorkItemMentionsAsync([Item(1)], Ct));
    }

    [Fact]
    public async Task Should_send_nothing_when_no_work_item_has_a_project()
    {
        using var fixture = new AzureDevOpsFixture();

        var mentions = await fixture.Service.GetWorkItemMentionsAsync([Item(1, project: "")], Ct);

        Assert.Empty(mentions);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task Should_let_a_failed_identity_lookup_surface_for_the_page_to_handle()
    {
        using var fixture = new AzureDevOpsFixture();
        fixture.ConnectionDataResponse = _ => Status(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.GetWorkItemMentionsAsync([Item(1)], Ct));
    }

    // --- Options ------------------------------------------------------------------------------

    [Fact]
    public void Should_read_the_organization_with_a_trailing_slash_and_the_distinct_projects()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureDevOps:OrganizationUrl"] = " https://dev.azure.com/cegid ",
            ["AzureDevOps:Projects:0"] = "Alpha",
            ["AzureDevOps:Projects:1"] = " alpha ",
            ["AzureDevOps:Projects:2"] = "",
            ["AzureDevOps:Projects:3"] = "Beta",
        }).Build();

        var options = AzureDevOpsOptions.FromConfiguration(configuration);

        Assert.Equal(new Uri("https://dev.azure.com/cegid/"), options.OrganizationUrl);
        Assert.Equal(["Alpha", "Beta"], options.Projects);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dev.azure.com/cegid")]
    [InlineData("ftp://dev.azure.com/cegid/")]
    public void Should_treat_a_missing_or_invalid_organization_url_as_not_configured(string? url)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureDevOps:OrganizationUrl"] = url })
            .Build();

        Assert.Null(AzureDevOpsOptions.FromConfiguration(configuration).OrganizationUrl);
    }
}
