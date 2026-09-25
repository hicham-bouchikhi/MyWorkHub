using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary><see cref="IAzureDevOpsService"/> over the Azure DevOps REST API (raw HTTP via <see cref="AzureDevOpsClient"/>).</summary>
internal sealed partial class AzureDevOpsService : IAzureDevOpsService
{
    private const string API_VERSION = "7.1";

    // The work item comments resource has been preview-only since API 5.1 and was never promoted to
    // GA; ".3" is its stable schema. If Microsoft promotes it, drop the suffix here (the only place).
    // A preview version gets a 12-week deprecation window before it is deactivated.
    // See docs/adr/0001-azdo-work-item-comments-preview-api.md.
    private const string COMMENTS_API_VERSION = "7.1-preview.3";

    // The work items endpoint accepts at most 200 ids per call.
    private const int WORK_ITEM_BATCH_SIZE = 200;

    private const string REF_HEADS_PREFIX = "refs/heads/";
    private const string EFFORT_FORMAT = "0.##";

    private const string TITLE_FIELD = "System.Title";
    private const string TYPE_FIELD = "System.WorkItemType";
    private const string STATE_FIELD = "System.State";
    private const string PROJECT_FIELD = "System.TeamProject";
    private const string PRIORITY_FIELD = "Microsoft.VSTS.Common.Priority";
    private const string DUE_DATE_FIELD = "Microsoft.VSTS.Scheduling.DueDate";
    private const string STORY_POINTS_FIELD = "Microsoft.VSTS.Scheduling.StoryPoints";
    private const string EFFORT_FIELD = "Microsoft.VSTS.Scheduling.Effort";

    /// <summary>Open items assigned to the user; <c>@Me</c> is resolved server-side.</summary>
    internal const string MY_OPEN_WORK_ITEMS_WIQL =
        "SELECT [System.Id] FROM WorkItems " +
        "WHERE [System.AssignedTo] = @Me " +
        "AND [System.State] NOT IN ('Closed', 'Removed', 'Done') " +
        "ORDER BY [System.ChangedDate] DESC";

    // Authored first: a pull request the user both created and reviews is listed as theirs.
    private static readonly PullRequestRole[] _roleQueryOrder = [PullRequestRole.AUTHOR, PullRequestRole.REVIEWER];

    private readonly AzureDevOpsClient _client;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(AzureDevOpsClient client, AzureDevOpsOptions options, ILogger<AzureDevOpsService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _options = options;
        _logger = logger;
    }

    // --- Pull requests ------------------------------------------------------------------

    public async Task<IReadOnlyList<PullRequestItem>> GetMyPullRequestsAsync(CancellationToken ct = default)
    {
        // First, so a missing token is reported (and a sign-in offered) before any configuration gap.
        var me = await _client.GetCurrentUserAsync(ct).ConfigureAwait(false);

        if (_options.Projects.Count == 0)
        {
            throw new InvalidOperationException(
                $"No Azure DevOps projects are configured. List them under {AzureDevOpsOptions.SECTION}:Projects " +
                "in ~/.MyWorkHub/appsettings.json.");
        }

        var byId = new Dictionary<int, PullRequestItem>();
        foreach (var project in _options.Projects)
        {
            foreach (var role in _roleQueryOrder)
            {
                foreach (var dto in await ListActivePullRequestsAsync(project, role, me.Id, ct).ConfigureAwait(false))
                {
                    byId.TryAdd(dto.PullRequestId, MapPullRequest(dto, project, role, me.Id));
                }
            }
        }

        return byId.Values.OrderByDescending(pr => pr.CreatedAt).ToList();
    }

    private async Task<IReadOnlyList<PullRequestDto>> ListActivePullRequestsAsync(
        string project, PullRequestRole role, string userId, CancellationToken ct)
    {
        var filter = role == PullRequestRole.AUTHOR ? "creatorId" : "reviewerId";
        var url = $"{Uri.EscapeDataString(project)}/_apis/git/pullrequests" +
                  $"?searchCriteria.status=active&searchCriteria.{filter}={Uri.EscapeDataString(userId)}" +
                  $"&api-version={API_VERSION}";

        var page = await _client.GetAsync(url, AzureDevOpsJson.Default.PullRequestListDto, ct).ConfigureAwait(false);
        return page?.Value ?? [];
    }

    private PullRequestItem MapPullRequest(PullRequestDto dto, string project, PullRequestRole role, string userId)
    {
        var repository = dto.Repository?.Name ?? "";
        var projectName = dto.Repository?.Project?.Name is { Length: > 0 } name ? name : project;
        var myVote = role == PullRequestRole.REVIEWER
            ? ToVote(dto.Reviewers?.FirstOrDefault(r => string.Equals(r.Id, userId, StringComparison.OrdinalIgnoreCase))?.Vote ?? 0)
            : PullRequestVote.NONE;

        return new PullRequestItem(
            dto.PullRequestId,
            dto.Title is { Length: > 0 } title ? title : "(untitled)",
            repository,
            projectName,
            dto.CreatedBy?.DisplayName ?? "",
            BranchName(dto.SourceRefName),
            BranchName(dto.TargetRefName),
            dto.CreationDate.UtcDateTime,
            role,
            myVote,
            dto.IsDraft,
            BrowserUrl(string.Create(CultureInfo.InvariantCulture,
                $"{Uri.EscapeDataString(projectName)}/_git/{Uri.EscapeDataString(repository)}/pullrequest/{dto.PullRequestId}")));
    }

    private static PullRequestVote ToVote(int vote) => vote switch
    {
        >= 10 => PullRequestVote.APPROVED,
        >= 5 => PullRequestVote.SUGGESTIONS,
        <= -10 => PullRequestVote.REJECTED,
        <= -5 => PullRequestVote.WAITING,
        _ => PullRequestVote.NONE,
    };

    private static string BranchName(string? refName)
        => refName is null ? ""
            : refName.StartsWith(REF_HEADS_PREFIX, StringComparison.Ordinal) ? refName[REF_HEADS_PREFIX.Length..]
            : refName;

    // --- Work items ---------------------------------------------------------------------

    public async Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default)
    {
        // Organization-scoped WIQL: covers every project without needing the Projects list.
        var result = await _client.PostAsync(
            $"_apis/wit/wiql?api-version={API_VERSION}",
            new WiqlRequestDto(MY_OPEN_WORK_ITEMS_WIQL),
            AzureDevOpsJson.Default.WiqlRequestDto,
            AzureDevOpsJson.Default.WiqlResponseDto,
            ct).ConfigureAwait(false);

        var ids = (result?.WorkItems ?? []).Select(w => w.Id).Distinct().ToList();

        var items = new List<WorkItem>(ids.Count);
        foreach (var batch in ids.Chunk(WORK_ITEM_BATCH_SIZE))
        {
            // No "fields" filter: requesting a field the organization's processes lack fails the call.
            var url = $"_apis/wit/workitems?ids={string.Join(',', batch)}&api-version={API_VERSION}";
            var page = await _client.GetAsync(url, AzureDevOpsJson.Default.WorkItemListDto, ct).ConfigureAwait(false);

            // The batch comes back in id order; re-apply the WIQL (most recently changed first) order.
            var byId = (page?.Value ?? []).ToDictionary(w => w.Id);
            items.AddRange(batch.Where(byId.ContainsKey).Select(id => MapWorkItem(byId[id])));
        }

        return items;
    }

    private WorkItem MapWorkItem(WorkItemDto dto)
    {
        var fields = dto.Fields ?? [];
        var project = Text(fields, PROJECT_FIELD);

        var effort = Number(fields, STORY_POINTS_FIELD) ?? Number(fields, EFFORT_FIELD);
        var priority = Number(fields, PRIORITY_FIELD);

        DateTime? dueDate = fields.TryGetValue(DUE_DATE_FIELD, out var due) && due.ValueKind == JsonValueKind.String
                            && due.TryGetDateTimeOffset(out var parsed)
            ? parsed.UtcDateTime
            : null;

        return new WorkItem(
            dto.Id,
            Text(fields, TITLE_FIELD),
            Text(fields, TYPE_FIELD),
            Text(fields, STATE_FIELD),
            priority?.ToString(CultureInfo.InvariantCulture) ?? "",
            dueDate,
            WorkItemUrl(project, dto.Id),
            effort?.ToString(EFFORT_FORMAT, CultureInfo.InvariantCulture) ?? "",
            project);
    }

    private static string Text(Dictionary<string, JsonElement> fields, string name)
        => fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static double? Number(Dictionary<string, JsonElement> fields, string name)
        => fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;

    private string WorkItemUrl(string project, int id)
        => BrowserUrl(project.Length > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{Uri.EscapeDataString(project)}/_workitems/edit/{id}")
            : string.Create(CultureInfo.InvariantCulture, $"_workitems/edit/{id}"));

    // --- Mentions (preview API, best-effort) ------------------------------------------------

    public async Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
        IReadOnlyList<WorkItem> workItems, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workItems);

        // Comments are queried per project; nothing to do (and no request at all) without one.
        var queryable = workItems.Where(w => w.Project.Length > 0).DistinctBy(w => w.Id).ToList();
        if (queryable.Count == 0)
        {
            return [];
        }

        var me = await _client.GetCurrentUserAsync(ct).ConfigureAwait(false);
        if (me.DisplayName.Length == 0)
        {
            return [];
        }

        var mentions = new List<WorkItemMention>();
        foreach (var workItem in queryable)
        {
            try
            {
                mentions.AddRange(await GetMentionsOnAsync(workItem, me.DisplayName, ct).ConfigureAwait(false));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Preview API: a 400/404 (or a timeout) on one item must not hide the others' mentions.
                LogCommentsSkipped(workItem.Id, ex);
            }
        }

        return mentions.OrderByDescending(m => m.CreatedAt).ToList();
    }

    private async Task<IEnumerable<WorkItemMention>> GetMentionsOnAsync(WorkItem workItem, string displayName, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{Uri.EscapeDataString(workItem.Project)}/_apis/wit/workItems/{workItem.Id}/comments?api-version={COMMENTS_API_VERSION}");
        var page = await _client.GetAsync(url, AzureDevOpsJson.Default.CommentListDto, ct).ConfigureAwait(false);

        return (page?.Comments ?? [])
            .Where(c => WorkItemMentionScanner.ContainsMentionOf(c.Text, displayName))
            .Select(c => new WorkItemMention(
                workItem.Id,
                workItem.Title,
                workItem.Url,
                c.Id,
                c.CreatedBy?.DisplayName ?? "",
                c.CreatedDate.UtcDateTime,
                WorkItemMentionScanner.ExtractSnippet(c.Text)));
    }

    private string BrowserUrl(string relative) => new Uri(_client.OrganizationUrl, relative).AbsoluteUri;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the comments of work item {WorkItemId}; skipping its mentions.")]
    private partial void LogCommentsSkipped(int workItemId, Exception exception);
}
