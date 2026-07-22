using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>
/// <see cref="IAzureDevOpsService"/> implemented against the Azure DevOps REST API
/// (HttpClient + PAT Basic auth), per the project decision to avoid the SDK
/// (T018–T020).
/// </summary>
public sealed partial class AzureDevOpsService : IAzureDevOpsService
{
    private const string API_VERSION = "7.1";

    // connectionData is a preview-only resource: it rejects the GA "7.1" with a 400
    // ("the -preview flag must be supplied"). It must be requested with a -preview version.
    private const string CONNECTION_API_VERSION = "7.1-preview.1";

    // The teams ($mine) resource is likewise preview-only.
    private const string TEAMS_API_VERSION = "7.1-preview.3";

    // Work item comments have been preview-only since API v5.1 and have never been promoted
    // to GA (as of July 2026). The schema (.3) is stable across major versions. If Microsoft
    // ever promotes this to GA, drop the "-preview.3" suffix. If it gets a 400 "deactivated"
    // response, there will have been a 12-week deprecation window first — watch the logs.
    // See docs/adr/0001-azdo-work-item-comments-preview-api.md for the full decision record.
    private const string COMMENTS_API_VERSION = "7.1-preview.3";

    private readonly HttpClient _http;
    private readonly AzureDevOpsOptions _options;
    private readonly ICredentialStore _credentials;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(
        HttpClient http,
        IOptions<AzureDevOpsOptions> options,
        ICredentialStore credentials,
        ILogger<AzureDevOpsService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentials);
        _http = http;
        _options = options.Value;
        _credentials = credentials;
        _logger = logger ?? NullLogger<AzureDevOpsService>.Instance;
    }

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure DevOps: could not load team review queues; showing personal PRs only.")]
    private partial void LogTeamQueueFailure(Exception exception);

    private string OrgBase => AzureDevOpsUrlBuilder.NormalizeBase(_options.OrganizationUrl);

    public async Task<IReadOnlyList<PullRequestItem>> GetPullRequestsForReviewAsync(CancellationToken ct = default)
    {
        var userId = await GetCurrentUserIdAsync(ct).ConfigureAwait(false);

        // Deduped by (project, PR id): a PR can be a personal review and a team review at once.
        var byKey = new Dictionary<(string Project, int Id), PullRequestItem>();

        // 1) Personal review queue across the configured projects.
        foreach (var project in _options.Projects)
        {
            await AddReviewQueueAsync(byKey, project, userId, userId, reviewerTeam: null, ct).ConfigureAwait(false);
        }

        // 2) Team review queues: each team the user belongs to, in that team's own project.
        //    Best-effort — a Teams failure (e.g. missing permission) must never break the
        //    personal queue.
        try
        {
            foreach (var team in await GetMyTeamsAsync(ct).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(team.Id) && !string.IsNullOrEmpty(team.ProjectName))
                {
                    await AddReviewQueueAsync(byKey, team.ProjectName, team.Id, userId, team.Name, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            LogTeamQueueFailure(ex);
        }

        return [.. byKey.Values];
    }

    private async Task AddReviewQueueAsync(
        Dictionary<(string Project, int Id), PullRequestItem> byKey,
        string project,
        string reviewerId,
        string currentUserId,
        string? reviewerTeam,
        CancellationToken ct)
    {
        var url = $"{OrgBase}{Uri.EscapeDataString(project)}/_apis/git/pullrequests" +
                  $"?searchCriteria.status=active&searchCriteria.reviewerId={Uri.EscapeDataString(reviewerId)}" +
                  $"&api-version={API_VERSION}";

        var payload = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.PrListResponse, ct)
            .ConfigureAwait(false);

        foreach (var pr in payload?.Value ?? [])
        {
            var key = (project, pr.PullRequestId);
            var item = MapPullRequest(pr, project, currentUserId, reviewerId, reviewerTeam);

            // A direct personal review always wins over a "via team" entry for the same PR.
            if (byKey.TryGetValue(key, out var existing) && existing.ReviewerTeam is null)
            {
                continue;
            }

            byKey[key] = item;
        }
    }

    private async Task<IReadOnlyList<TeamDto>> GetMyTeamsAsync(CancellationToken ct)
    {
        var url = $"{OrgBase}_apis/teams?$mine=true&api-version={TEAMS_API_VERSION}";
        var payload = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.TeamListResponse, ct)
            .ConfigureAwait(false);
        return payload?.Value ?? [];
    }

    public async Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default)
    {
        // @CurrentIteration in the WIQL is a team-relative macro: it only resolves when the
        // query is POSTed to a TEAM-scoped endpoint. Resolve a team per project — the user's
        // own team (most accurate for "my current sprint"), falling back to the project's
        // default team — otherwise the project-scoped query returns nothing.
        var teamByProject = await BuildTeamByProjectAsync(ct).ConfigureAwait(false);

        var results = new List<WorkItem>();
        var seen = new HashSet<int>();
        foreach (var project in _options.Projects)
        {
            var team = teamByProject.GetValueOrDefault(project)
                       ?? await GetProjectDefaultTeamAsync(project, ct).ConfigureAwait(false);

            var ids = await RunWiqlAsync(project, team, ct).ConfigureAwait(false);
            if (ids.Count == 0)
            {
                continue;
            }

            var url = $"{OrgBase}{Uri.EscapeDataString(project)}/_apis/wit/workitems" +
                      $"?ids={string.Join(',', ids)}&api-version={API_VERSION}";

            var payload = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.WorkItemListResponse, ct)
                .ConfigureAwait(false);

            foreach (var wi in payload?.Value ?? [])
            {
                if (seen.Add(wi.Id))
                {
                    results.Add(MapWorkItem(wi, project));
                }
            }
        }

        return results;
    }

    /// <summary>Maps each project to one of the user's teams in it (best-effort; empty on failure).</summary>
    private async Task<Dictionary<string, string>> BuildTeamByProjectAsync(CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var team in await GetMyTeamsAsync(ct).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(team.ProjectName) && !string.IsNullOrEmpty(team.Name) &&
                    !map.ContainsKey(team.ProjectName))
                {
                    map[team.ProjectName] = team.Name;
                }
            }
        }
        catch (Exception ex)
        {
            LogTeamQueueFailure(ex);
        }

        return map;
    }

    /// <summary>The project's default team name, used as a fallback team context for @CurrentIteration.</summary>
    private async Task<string?> GetProjectDefaultTeamAsync(string project, CancellationToken ct)
    {
        try
        {
            var url = $"{OrgBase}_apis/projects/{Uri.EscapeDataString(project)}?api-version={API_VERSION}";
            var info = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.ProjectInfo, ct)
                .ConfigureAwait(false);
            return info?.DefaultTeam?.Name;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<string> GetCurrentUserIdAsync(CancellationToken ct)
    {
        var (id, _) = await GetCurrentUserInfoAsync(ct).ConfigureAwait(false);
        return id;
    }

    private async Task<(string Id, string DisplayName)> GetCurrentUserInfoAsync(CancellationToken ct)
    {
        var url = $"{OrgBase}_apis/connectionData?api-version={CONNECTION_API_VERSION}";
        var data = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.ConnectionData, ct)
            .ConfigureAwait(false);
        var user = data?.AuthenticatedUser;
        return (user?.Id ?? "", user?.ProviderDisplayName ?? "");
    }

    private async Task<IReadOnlyList<int>> RunWiqlAsync(string project, string? team, CancellationToken ct)
    {
        // Team-scoped endpoint when a team is known, so @CurrentIteration resolves.
        var scope = string.IsNullOrEmpty(team)
            ? Uri.EscapeDataString(project)
            : $"{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(team)}";

        var url = $"{OrgBase}{scope}/_apis/wit/wiql?api-version={API_VERSION}";
        var body = JsonContent.Create(new WiqlQuery(WorkItemWiql.CurrentSprintAssignedToMe), AzureDevOpsJson.Default.WiqlQuery);

        var payload = await SendAsync(HttpMethod.Post, url, body, AzureDevOpsJson.Default.WiqlResponse, ct)
            .ConfigureAwait(false);

        return (payload?.WorkItems ?? []).Select(static w => w.Id).ToList();
    }

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string url,
        HttpContent? content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        var pat = AzureDevOpsAuth.GetPatOrThrow(_credentials);
        request.Headers.TryAddWithoutValidation("Authorization", AzureDevOpsAuth.BuildBasicAuthHeaderValue(pat));

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Azure DevOps puts a useful JSON error in the body (e.g. wrong api-version,
            // bad project name). Surface it so failures are diagnosable from the log.
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var snippet = body.Length > 600 ? body[..600] : body;
            throw new HttpRequestException(
                $"Azure DevOps {method.Method} {request.RequestUri} failed: {(int)response.StatusCode} {response.StatusCode}. {snippet}");
        }

        return await response.Content.ReadFromJsonAsync(typeInfo, ct).ConfigureAwait(false);
    }

    private PullRequestItem MapPullRequest(PrDto pr, string project, string currentUserId, string matchedReviewerId, string? reviewerTeam)
    {
        var repository = pr.Repository?.Name ?? "";

        int VoteFor(string id) => pr.Reviewers?
            .FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase))?.Vote ?? 0;

        // Prefer the user's own vote; for a "via team" PR they may not be an individual
        // reviewer, so fall back to the matched team's (aggregate) vote.
        var userVote = VoteFor(currentUserId);
        var vote = userVote != 0 ? userVote : VoteFor(matchedReviewerId);

        return new PullRequestItem(
            Id: pr.PullRequestId,
            Title: pr.Title is { Length: > 0 } title ? title : "(untitled)",
            Author: pr.CreatedBy?.DisplayName ?? "(unknown)",
            Repository: repository,
            CreatedAt: pr.CreationDate.UtcDateTime,
            VoteStatus: PullRequestVote.Describe(vote),
            Url: AzureDevOpsUrlBuilder.BuildPullRequestUrl(OrgBase, project, repository, pr.PullRequestId),
            ReviewerTeam: reviewerTeam)
        {
            ProjectName = pr.Repository?.Project?.Name ?? project,
            SourceBranch = StripRefPrefix(pr.SourceRefName),
            TargetBranch = StripRefPrefix(pr.TargetRefName),
            // Prefer the server-provided remote URL; fall back to the deterministic AzDO
            // clone URL when the PR-list response omits it (some orgs return no remoteUrl).
            CloneUrl = pr.Repository?.RemoteUrl is { Length: > 0 } remote
                ? remote
                : AzureDevOpsUrlBuilder.BuildCloneUrl(OrgBase, project, repository),
        };
    }

    /// <summary>Strips the <c>refs/heads/</c> prefix from a full ref name, leaving the branch short name.</summary>
    private static string StripRefPrefix(string? refName)
    {
        const string headsPrefix = "refs/heads/";
        if (string.IsNullOrEmpty(refName))
        {
            return "";
        }

        return refName.StartsWith(headsPrefix, StringComparison.Ordinal)
            ? refName[headsPrefix.Length..]
            : refName;
    }

    public async Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
        IReadOnlyList<WorkItem> workItems, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        // Filter upfront: the comments endpoint is project-scoped, so items without a project
        // name cannot be queried. Skip the connectionData call entirely if nothing is queryable.
        var queryable = workItems.Where(static wi => !string.IsNullOrEmpty(wi.Project)).ToList();
        if (queryable.Count == 0)
        {
            return [];
        }

        var (_, displayName) = await GetCurrentUserInfoAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(displayName))
        {
            return [];
        }

        var results = new List<WorkItemMention>();

        foreach (var wi in queryable)
        {
            try
            {
                var url = $"{OrgBase}{Uri.EscapeDataString(wi.Project)}/_apis/wit/workItems/{wi.Id}/comments" +
                          $"?api-version={COMMENTS_API_VERSION}";

                var payload = await SendAsync(HttpMethod.Get, url, content: null,
                    AzureDevOpsJson.Default.WorkItemCommentsResponse, ct).ConfigureAwait(false);

                foreach (var comment in payload?.Comments ?? [])
                {
                    if (WorkItemMentionScanner.ContainsMentionOf(comment.Text, displayName))
                    {
                        results.Add(new WorkItemMention(
                            WorkItemId: wi.Id,
                            WorkItemTitle: wi.Title,
                            WorkItemUrl: wi.Url,
                            CommentId: comment.Id,
                            AuthorDisplayName: comment.CreatedBy?.DisplayName ?? "(unknown)",
                            CreatedAt: comment.CreatedDate,
                            TextSnippet: WorkItemMentionScanner.ExtractSnippet(comment.Text)));
                    }
                }
            }
            catch (Exception ex)
            {
                LogCommentFetchFailure(ex, wi.Id);
            }
        }

        return results;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Azure DevOps: could not fetch comments for work item {WorkItemId}; skipping mentions.")]
    private partial void LogCommentFetchFailure(Exception exception, int workItemId);

    public async Task<WorkItemDetails?> GetWorkItemDetailsAsync(int id, CancellationToken ct = default)
    {
        var url = $"{OrgBase}_apis/wit/workitems/{id}?$expand=fields&api-version={API_VERSION}";
        var dto = await SendAsync(HttpMethod.Get, url, content: null, AzureDevOpsJson.Default.WorkItemDto, ct)
            .ConfigureAwait(false);
        if (dto is null)
        {
            return null;
        }

        var fields = dto.Fields ?? [];
        string Field(string key) =>
            fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? ""
                : "";

        return new WorkItemDetails(
            Id: dto.Id,
            Description: StripHtmlToPlainText(Field("System.Description")),
            AcceptanceCriteria: StripHtmlToPlainText(Field("Microsoft.VSTS.Common.AcceptanceCriteria")));
    }

    public async Task UpdateWorkItemAsync(int id, string? description, string? acceptanceCriteria, CancellationToken ct = default)
    {
        var ops = new List<JsonPatchOp>();
        if (description is not null)
        {
            ops.Add(new JsonPatchOp("add", "/fields/System.Description", PlainTextToHtml(description)));
        }

        if (acceptanceCriteria is not null)
        {
            ops.Add(new JsonPatchOp("add", "/fields/Microsoft.VSTS.Common.AcceptanceCriteria", PlainTextToHtml(acceptanceCriteria)));
        }

        if (ops.Count == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(ops, AzureDevOpsJson.Default.ListJsonPatchOp);
        using var content = new StringContent(json, Encoding.UTF8, "application/json-patch+json");
        var url = $"{OrgBase}_apis/wit/workitems/{id}?api-version={API_VERSION}";
        await SendAsync(HttpMethod.Patch, url, content, AzureDevOpsJson.Default.WorkItemDto, ct)
            .ConfigureAwait(false);
    }

    private static string StripHtmlToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = html
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</p>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</div>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("</li>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<li>", "• ", StringComparison.OrdinalIgnoreCase);

        text = HtmlTagRegex().Replace(text, "");

        text = text
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
            .Replace("&apos;", "'", StringComparison.OrdinalIgnoreCase)
            .Replace("&#160;", " ", StringComparison.Ordinal);

        var sb = new StringBuilder();
        var blankRun = 0;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                blankRun++;
                if (blankRun <= 1)
                {
                    sb.AppendLine();
                }
            }
            else
            {
                blankRun = 0;
                sb.AppendLine(trimmed);
            }
        }

        return sb.ToString().Trim();
    }

    private static string PlainTextToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrEmpty(line))
            {
                sb.Append("<p>")
                  .Append(WebUtility.HtmlEncode(line))
                  .AppendLine("</p>");
            }
        }

        return sb.ToString();
    }

    private WorkItem MapWorkItem(WorkItemDto wi, string project)
    {
        var fields = wi.Fields ?? [];

        string Text(string key) =>
            fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        var priority = fields.TryGetValue("Microsoft.VSTS.Common.Priority", out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32().ToString(CultureInfo.InvariantCulture)
            : "";

        // Effort lives under different fields per work-item type: Story Points for
        // stories/PBIs, Effort for backlog items. Take whichever is present.
        string Estimate(string key) =>
            fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble().ToString("0.##", CultureInfo.InvariantCulture)
                : "";

        var effort = Estimate("Microsoft.VSTS.Scheduling.StoryPoints");
        if (effort.Length == 0)
        {
            effort = Estimate("Microsoft.VSTS.Scheduling.Effort");
        }

        DateTime? dueDate = null;
        if (fields.TryGetValue("Microsoft.VSTS.Scheduling.DueDate", out var d) &&
            d.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(d.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            dueDate = parsed;
        }

        return new WorkItem(
            Id: wi.Id,
            Title: Text("System.Title"),
            Type: Text("System.WorkItemType"),
            State: Text("System.State"),
            Priority: priority,
            DueDate: dueDate,
            Url: AzureDevOpsUrlBuilder.BuildWorkItemUrl(OrgBase, project, wi.Id),
            Effort: effort,
            Project: project);
    }
}
