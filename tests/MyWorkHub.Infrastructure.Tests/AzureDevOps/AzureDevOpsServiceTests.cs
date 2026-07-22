using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Infrastructure.AzureDevOps;
using MyWorkHub.Infrastructure.Tests.TestDoubles;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.Tests.AzureDevOps;

public sealed class AzureDevOpsServiceTests
{
    private const string USER_ID = "11111111-aaaa-bbbb-cccc-222222222222";
    private const string ORG = "https://dev.azure.com/cegid/";

    // USER_ID is inlined into the JSON below (interpolated raw strings collide with JSON's "}}").
    private const string CONNECTION_DATA_JSON = """{"authenticatedUser":{"id":"11111111-aaaa-bbbb-cccc-222222222222","providerDisplayName":"Hicham BOUCHIKHI"}}""";

    private const string PULL_REQUESTS_JSON = """
        {"value":[{"pullRequestId":42,"title":"Fix the bug","createdBy":{"displayName":"Jane Dev"},
        "repository":{"name":"MyRepo"},"creationDate":"2026-06-18T10:00:00Z",
        "reviewers":[{"id":"11111111-aaaa-bbbb-cccc-222222222222","vote":10},{"id":"someone-else","vote":0}]}]}
        """;

    private const string WIQL_JSON = """{"workItems":[{"id":101},{"id":102}]}""";

    private const string WORK_ITEMS_JSON = """
        {"count":2,"value":[
        {"id":101,"fields":{"System.Title":"Implement feature","System.WorkItemType":"User Story",
        "System.State":"Active","Microsoft.VSTS.Common.Priority":2,
        "Microsoft.VSTS.Scheduling.StoryPoints":5,
        "Microsoft.VSTS.Scheduling.DueDate":"2026-06-30T00:00:00Z"}},
        {"id":102,"fields":{"System.Title":"Bug fix","System.WorkItemType":"Bug","System.State":"New"}}]}
        """;

    private static AzureDevOpsService BuildService(HttpClient http, bool withPat = true)
    {
        var store = new FakeCredentialStore();
        if (withPat)
        {
            store.Save(CredentialKeys.AZURE_DEVOPS_PAT, "test-pat");
        }

        var options = new AzureDevOpsOptions { OrganizationUrl = ORG };
        options.Projects.Add("MyProject");

        return new AzureDevOpsService(http, Options.Create(options), store);
    }

    private static HttpResponseMessage Route(HttpRequestMessage request)
    {
        var url = request.RequestUri!.ToString();
        if (url.Contains("connectionData", StringComparison.Ordinal))
        {
            return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
        }

        if (url.Contains("/git/pullrequests", StringComparison.Ordinal))
        {
            return StubHttpMessageHandler.Json(PULL_REQUESTS_JSON);
        }

        if (url.Contains("/wit/wiql", StringComparison.Ordinal))
        {
            return StubHttpMessageHandler.Json(WIQL_JSON);
        }

        if (url.Contains("/wit/workitems", StringComparison.Ordinal))
        {
            return StubHttpMessageHandler.Json(WORK_ITEMS_JSON);
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_maps_the_current_users_review_queue()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var prs = await service.GetPullRequestsForReviewAsync(CancellationToken.None);

        var pr = Assert.Single(prs);
        Assert.Equal(42, pr.Id);
        Assert.Equal("Fix the bug", pr.Title);
        Assert.Equal("Jane Dev", pr.Author);
        Assert.Equal("MyRepo", pr.Repository);
        Assert.Equal("Approved", pr.VoteStatus);
        Assert.Equal(new DateTime(2026, 6, 18, 10, 0, 0, DateTimeKind.Utc), pr.CreatedAt);
        Assert.Equal("https://dev.azure.com/cegid/MyProject/_git/MyRepo/pullrequest/42", pr.Url);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_maps_branches_clone_url_and_project()
    {
        const string prJson = """
            {"value":[{"pullRequestId":42,"title":"Fix the bug","createdBy":{"displayName":"Jane Dev"},
            "repository":{"name":"MyRepo","remoteUrl":"https://cegid@dev.azure.com/cegid/MyProject/_git/MyRepo",
            "project":{"name":"MyProject"}},
            "creationDate":"2026-06-18T10:00:00Z","sourceRefName":"refs/heads/feature/x","targetRefName":"refs/heads/main",
            "reviewers":[{"id":"11111111-aaaa-bbbb-cccc-222222222222","vote":10}]}]}
            """;

        HttpResponseMessage Route2(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/git/pullrequests", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(prJson);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(Route2);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var pr = Assert.Single(await service.GetPullRequestsForReviewAsync(CancellationToken.None));
        Assert.Equal("feature/x", pr.SourceBranch);
        Assert.Equal("main", pr.TargetBranch);
        Assert.Equal("MyProject", pr.ProjectName);
        Assert.Equal("https://cegid@dev.azure.com/cegid/MyProject/_git/MyRepo", pr.CloneUrl);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_builds_a_clone_url_when_remote_url_is_missing()
    {
        // Repository object without remoteUrl (some AzDO orgs omit it from the PR list).
        const string prJson = """
            {"value":[{"pullRequestId":42,"title":"Fix","createdBy":{"displayName":"Jane"},
            "repository":{"name":"MyRepo"},"creationDate":"2026-06-18T10:00:00Z",
            "sourceRefName":"refs/heads/feature/x","targetRefName":"refs/heads/main",
            "reviewers":[{"id":"11111111-aaaa-bbbb-cccc-222222222222","vote":10}]}]}
            """;

        HttpResponseMessage Route2(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/git/pullrequests", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(prJson);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(Route2);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var pr = Assert.Single(await service.GetPullRequestsForReviewAsync(CancellationToken.None));
        Assert.Equal("https://dev.azure.com/cegid/MyProject/_git/MyRepo", pr.CloneUrl);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_queries_with_the_current_user_and_pat()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        await service.GetPullRequestsForReviewAsync(CancellationToken.None);

        var prRequest = handler.Requests.Single(r => r.RequestUri!.ToString().Contains("/git/pullrequests", StringComparison.Ordinal));
        Assert.Contains($"reviewerId={USER_ID}", prRequest.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Equal("Basic", prRequest.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_calls_connectionData_with_a_preview_api_version()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        await service.GetPullRequestsForReviewAsync(CancellationToken.None);

        // connectionData is preview-only; requesting GA "7.1" returns 400 (regression guard).
        var connection = handler.Requests.Single(r => r.RequestUri!.ToString().Contains("connectionData", StringComparison.Ordinal));
        Assert.Contains("api-version=7.1-preview", connection.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_includes_the_response_body_when_a_request_fails()
    {
        const string errorBody = """{"message":"The requested version \"7.1\" of the resource is under preview."}""";
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
        {
            Content = new StringContent(errorBody),
        });
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetPullRequestsForReviewAsync(CancellationToken.None));

        Assert.Contains("under preview", ex.Message, StringComparison.Ordinal);
        Assert.Contains("400", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_merges_team_queues_labeled_via_team()
    {
        const string teamId = "33333333-aaaa-bbbb-cccc-444444444444";
        const string teamsJson = $$"""{"count":1,"value":[{"id":"{{teamId}}","name":"Platform DevX","projectName":"MyProject"}]}""";
        const string personalPrJson = """
            {"value":[{"pullRequestId":42,"title":"Mine","createdBy":{"displayName":"Jane"},
            "repository":{"name":"R"},"creationDate":"2026-06-18T10:00:00Z",
            "reviewers":[{"id":"11111111-aaaa-bbbb-cccc-222222222222","vote":10}]}]}
            """;
        const string teamPrJson = $$"""
            {"value":[{"pullRequestId":99,"title":"Team","createdBy":{"displayName":"Bob"},
            "repository":{"name":"R"},"creationDate":"2026-06-19T10:00:00Z",
            "reviewers":[{"id":"{{teamId}}","vote":5}]}]}
            """;

        HttpResponseMessage Route2(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/_apis/teams", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(teamsJson);
            if (url.Contains("/git/pullrequests", StringComparison.Ordinal))
            {
                return url.Contains($"reviewerId={teamId}", StringComparison.Ordinal)
                    ? StubHttpMessageHandler.Json(teamPrJson)
                    : StubHttpMessageHandler.Json(personalPrJson);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(Route2);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var prs = await service.GetPullRequestsForReviewAsync(CancellationToken.None);

        Assert.Equal(2, prs.Count);

        var mine = prs.Single(p => p.Id == 42);
        Assert.Null(mine.ReviewerTeam);

        var team = prs.Single(p => p.Id == 99);
        Assert.Equal("Platform DevX", team.ReviewerTeam);
        Assert.Equal("Approved with suggestions", team.VoteStatus); // falls back to the team's vote
    }

    [Fact]
    public async Task GetMyWorkItemsAsync_runs_wiql_then_maps_the_returned_items()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var items = await service.GetMyWorkItemsAsync(CancellationToken.None);

        Assert.Equal(2, items.Count);

        var story = items[0];
        Assert.Equal(101, story.Id);
        Assert.Equal("Implement feature", story.Title);
        Assert.Equal("User Story", story.Type);
        Assert.Equal("Active", story.State);
        Assert.Equal("2", story.Priority);
        Assert.Equal("5", story.Effort);
        Assert.Equal(new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), story.DueDate);
        Assert.Equal("https://dev.azure.com/cegid/MyProject/_workitems/edit/101", story.Url);

        var bug = items[1];
        Assert.Equal(102, bug.Id);
        Assert.Equal("", bug.Priority);
        Assert.Null(bug.DueDate);
    }

    [Fact]
    public async Task GetMyWorkItemsAsync_runs_wiql_against_the_users_team_so_CurrentIteration_resolves()
    {
        const string teamsJson = """{"count":1,"value":[{"id":"t1","name":"My Team","projectName":"MyProject"}]}""";

        HttpResponseMessage Route2(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/_apis/teams", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(teamsJson);
            if (url.Contains("/wit/wiql", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(WIQL_JSON);
            if (url.Contains("/wit/workitems", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(WORK_ITEMS_JSON);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(Route2);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var items = await service.GetMyWorkItemsAsync(CancellationToken.None);

        Assert.Equal(2, items.Count);
        var wiql = handler.Requests.Single(r => r.RequestUri!.AbsoluteUri.Contains("/wit/wiql", StringComparison.Ordinal));
        Assert.Contains("/MyProject/My%20Team/_apis/wit/wiql", wiql.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMyWorkItemsAsync_posts_the_current_sprint_wiql()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        await service.GetMyWorkItemsAsync(CancellationToken.None);

        var wiqlRequest = handler.Requests.Single(r => r.RequestUri!.ToString().Contains("/wit/wiql", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, wiqlRequest.Method);
        var body = handler.BodyFor(wiqlRequest);
        Assert.Contains("@CurrentIteration", body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPullRequestsForReviewAsync_throws_when_no_pat_is_configured()
    {
        using var handler = new StubHttpMessageHandler(Route);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http, withPat: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetPullRequestsForReviewAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetWorkItemMentionsAsync_returns_mentions_matching_display_name()
    {
        const string commentsJson = """
            {"count":2,"comments":[
            {"id":501,"text":"<div>@Hicham BOUCHIKHI can you review?</div>",
            "createdDate":"2026-07-09T08:00:00Z","createdBy":{"displayName":"Alice Dev"}},
            {"id":502,"text":"<div>@Someone Else please check</div>",
            "createdDate":"2026-07-09T09:00:00Z","createdBy":{"displayName":"Bob"}}
            ]}
            """;

        HttpResponseMessage MentionRoute(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/comments", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(commentsJson);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(MentionRoute);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var workItems = new[]
        {
            new MyWorkHub.Core.Models.WorkItem(101, "Fix the bug", "Bug", "Active", "2", null,
                $"{ORG}MyProject/_workitems/edit/101", Project: "MyProject"),
        };

        var mentions = await service.GetWorkItemMentionsAsync(workItems, CancellationToken.None);

        var mention = Assert.Single(mentions);
        Assert.Equal(501, mention.CommentId);
        Assert.Equal(101, mention.WorkItemId);
        Assert.Equal("Alice Dev", mention.AuthorDisplayName);
        Assert.Contains("Hicham BOUCHIKHI", mention.TextSnippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetWorkItemMentionsAsync_returns_empty_when_no_matching_comments()
    {
        const string commentsJson = """
            {"count":1,"comments":[
            {"id":601,"text":"<div>Great progress on this!</div>",
            "createdDate":"2026-07-09T08:00:00Z","createdBy":{"displayName":"Alice"}}
            ]}
            """;

        HttpResponseMessage MentionRoute(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("connectionData", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(CONNECTION_DATA_JSON);
            if (url.Contains("/comments", StringComparison.Ordinal)) return StubHttpMessageHandler.Json(commentsJson);
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }

        using var handler = new StubHttpMessageHandler(MentionRoute);
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        var workItems = new[]
        {
            new MyWorkHub.Core.Models.WorkItem(101, "Fix", "Bug", "Active", "2", null,
                $"{ORG}MyProject/_workitems/edit/101", Project: "MyProject"),
        };

        var mentions = await service.GetWorkItemMentionsAsync(workItems, CancellationToken.None);

        Assert.Empty(mentions);
    }

    [Fact]
    public async Task GetWorkItemMentionsAsync_skips_items_without_project_set()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler, disposeHandler: false);
        var service = BuildService(http);

        // WorkItem without Project field (defaults to empty string)
        var workItems = new[]
        {
            new MyWorkHub.Core.Models.WorkItem(101, "Fix", "Bug", "Active", "2", null,
                $"{ORG}MyProject/_workitems/edit/101"),
        };

        var mentions = await service.GetWorkItemMentionsAsync(workItems, CancellationToken.None);

        Assert.Empty(mentions);
    }
}
