using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyWorkHub.Infrastructure.AzureDevOps;

// REST payload shapes (Azure DevOps API 7.1). Source-generated serialization keeps
// the IL2026/IL3050 trim/AOT analyzers (promoted to errors in .editorconfig) happy.

internal sealed record PrListResponse(IReadOnlyList<PrDto>? Value);

internal sealed record PrDto(
    int PullRequestId,
    string? Title,
    PrIdentity? CreatedBy,
    PrRepository? Repository,
    DateTimeOffset CreationDate,
    IReadOnlyList<PrReviewer>? Reviewers,
    string? SourceRefName,
    string? TargetRefName);

internal sealed record PrIdentity(string? DisplayName);

internal sealed record PrRepository(string? Name, string? RemoteUrl, PrProject? Project);

internal sealed record PrProject(string? Name);

internal sealed record PrReviewer(string? Id, int Vote);

internal sealed record WiqlQuery(string Query);

internal sealed record WiqlResponse(IReadOnlyList<WiqlRef>? WorkItems);

internal sealed record WiqlRef(int Id);

internal sealed record WorkItemListResponse(IReadOnlyList<WorkItemDto>? Value);

internal sealed record WorkItemDto(int Id, Dictionary<string, JsonElement>? Fields);

internal sealed record ConnectionData(ConnectionUser? AuthenticatedUser);

internal sealed record ConnectionUser(string? Id, string? ProviderDisplayName);

internal sealed record WorkItemCommentsResponse(IReadOnlyList<WorkItemCommentDto>? Comments);

// PrIdentity already has DisplayName — reused here for the comment author.
internal sealed record WorkItemCommentDto(int Id, string? Text, DateTime CreatedDate, PrIdentity? CreatedBy);

internal sealed record TeamListResponse(IReadOnlyList<TeamDto>? Value);

internal sealed record TeamDto(string? Id, string? Name, string? ProjectName);

internal sealed record ProjectInfo(TeamRef? DefaultTeam);

internal sealed record TeamRef(string? Id, string? Name);

internal sealed record JsonPatchOp(string Op, string Path, string? Value);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PrListResponse))]
[JsonSerializable(typeof(WiqlQuery))]
[JsonSerializable(typeof(WiqlResponse))]
[JsonSerializable(typeof(WorkItemListResponse))]
[JsonSerializable(typeof(WorkItemDto))]
[JsonSerializable(typeof(ConnectionData))]
[JsonSerializable(typeof(TeamListResponse))]
[JsonSerializable(typeof(ProjectInfo))]
[JsonSerializable(typeof(WorkItemCommentsResponse))]
[JsonSerializable(typeof(List<JsonPatchOp>))]
internal sealed partial class AzureDevOpsJson : JsonSerializerContext;
