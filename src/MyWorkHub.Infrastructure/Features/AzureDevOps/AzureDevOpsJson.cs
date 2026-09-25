using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

// Azure DevOps REST payload shapes — only the fields the app reads. Source-generated serialization
// keeps the whole slice reflection-free (the IL2026/IL3050 trim/AOT analyzers stay a tripwire).

internal sealed record ConnectionDataDto(ConnectionUserDto? AuthenticatedUser);

internal sealed record ConnectionUserDto(string? Id, string? ProviderDisplayName);

internal sealed record IdentityRefDto(string? Id, string? DisplayName);

internal sealed record PullRequestListDto(IReadOnlyList<PullRequestDto>? Value);

internal sealed record PullRequestDto(
    int PullRequestId,
    string? Title,
    IdentityRefDto? CreatedBy,
    RepositoryDto? Repository,
    DateTimeOffset CreationDate,
    IReadOnlyList<ReviewerDto>? Reviewers,
    string? SourceRefName,
    string? TargetRefName,
    bool IsDraft);

internal sealed record RepositoryDto(string? Name, ProjectRefDto? Project);

internal sealed record ProjectRefDto(string? Name);

internal sealed record ReviewerDto(string? Id, int Vote);

internal sealed record WiqlRequestDto(string Query);

internal sealed record WiqlResponseDto(IReadOnlyList<WorkItemRefDto>? WorkItems);

internal sealed record WorkItemRefDto(int Id);

internal sealed record WorkItemListDto(IReadOnlyList<WorkItemDto>? Value);

internal sealed record WorkItemDto(int Id, Dictionary<string, JsonElement>? Fields);

internal sealed record CommentListDto(IReadOnlyList<CommentDto>? Comments);

internal sealed record CommentDto(int Id, string? Text, IdentityRefDto? CreatedBy, DateTimeOffset CreatedDate);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ConnectionDataDto))]
[JsonSerializable(typeof(PullRequestListDto))]
[JsonSerializable(typeof(WiqlRequestDto))]
[JsonSerializable(typeof(WiqlResponseDto))]
[JsonSerializable(typeof(WorkItemListDto))]
[JsonSerializable(typeof(CommentListDto))]
internal sealed partial class AzureDevOpsJson : JsonSerializerContext;
