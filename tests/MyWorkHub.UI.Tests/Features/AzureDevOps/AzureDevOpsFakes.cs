using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.UI.Tests.Features.AzureDevOps;

/// <summary>Scriptable token store. Connecting with <see cref="VALID_TOKEN"/> flips <see cref="HasToken"/> for every fake sharing it.</summary>
internal sealed class FakeAzureDevOpsConnection : IAzureDevOpsConnectionService
{
    public const string VALID_TOKEN = "good-pat";

    public bool HasToken { get; set; } = true;

    public bool HasStoredToken => HasToken;

    public List<string> ConnectAttempts { get; } = [];

    public Task<AzureDevOpsConnectResult> ConnectAsync(string personalAccessToken, CancellationToken ct = default)
    {
        ConnectAttempts.Add(personalAccessToken);
        if (personalAccessToken != VALID_TOKEN)
        {
            return Task.FromResult(AzureDevOpsConnectResult.Failure("Azure DevOps rejected the personal access token."));
        }

        HasToken = true;
        return Task.FromResult(AzureDevOpsConnectResult.Success("Jane Doe"));
    }

    public void Disconnect() => HasToken = false;

    /// <summary>Throws like the real services do when no token is stored.</summary>
    public void ThrowIfNoToken()
    {
        if (!HasToken)
        {
            throw new AzureDevOpsNotConnectedException();
        }
    }
}

internal sealed class FakeAzureDevOpsService(FakeAzureDevOpsConnection connection) : IAzureDevOpsService
{
    public List<PullRequestItem> PullRequests { get; } = [];

    public List<WorkItem> WorkItems { get; } = [];

    public List<WorkItemMention> Mentions { get; } = [];

    /// <summary>When set, every list call throws it.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, only the mentions call throws it.</summary>
    public Exception? MentionsFailure { get; set; }

    public IReadOnlyList<WorkItem>? MentionsRequestedFor { get; private set; }

    public static PullRequestItem PullRequest(int id, PullRequestRole role = PullRequestRole.REVIEWER)
        => new(id, "PR " + id, "web", "Alpha", "Alice", "feature/x", "main",
            new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc), role, PullRequestVote.NONE, IsDraft: false,
            "https://dev.azure.com/cegid/Alpha/_git/web/pullrequest/" + id);

    public static WorkItem WorkItem(int id)
        => new(id, "Item " + id, "Task", "Active", "2", null, "https://dev.azure.com/cegid/Alpha/_workitems/edit/" + id, "3", "Alpha");

    public static WorkItemMention Mention(int commentId, int workItemId = 1)
        => new(workItemId, "Item " + workItemId, "https://dev.azure.com/cegid/Alpha/_workitems/edit/" + workItemId, commentId,
            "Bob", new DateTime(2026, 9, 24, 9, 0, 0, DateTimeKind.Utc), "@Jane Doe have a look");

    /// <summary>When set, pull-request fetches wait for this gate or for cancellation (to simulate a slow load).</summary>
    public TaskCompletionSource? PullRequestsGate { get; set; }

    public async Task<IReadOnlyList<PullRequestItem>> GetMyPullRequestsAsync(CancellationToken ct = default)
    {
        ThrowIfFailing();
        if (PullRequestsGate is { } gate)
        {
            await gate.Task.WaitAsync(ct);
        }

        return [.. PullRequests];
    }

    public Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default)
    {
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyList<WorkItem>>([.. WorkItems]);
    }

    public Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(IReadOnlyList<WorkItem> workItems, CancellationToken ct = default)
    {
        ThrowIfFailing();
        MentionsRequestedFor = workItems;
        if (MentionsFailure is not null)
        {
            throw MentionsFailure;
        }

        return Task.FromResult<IReadOnlyList<WorkItemMention>>([.. Mentions]);
    }

    private void ThrowIfFailing()
    {
        connection.ThrowIfNoToken();
        if (Failure is not null)
        {
            throw Failure;
        }
    }
}

internal sealed class FakeSeenMentionRepository : ISeenMentionRepository
{
    public HashSet<int> Seen { get; } = [];

    public Exception? Failure { get; set; }

    public int MarkCalls { get; private set; }

    public Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult<IReadOnlySet<int>>(new HashSet<int>(Seen));
    }

    public Task MarkSeenAsync(int commentId, CancellationToken ct = default)
    {
        MarkCalls++;
        if (Failure is not null)
        {
            throw Failure;
        }

        Seen.Add(commentId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeBrowserLauncher : IBrowserLauncher
{
    public List<string> Opened { get; } = [];

    public void Open(string url) => Opened.Add(url);
}
