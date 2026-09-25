using System.Text.Json;
using System.Text.Json.Serialization;
using MyWorkHub.Core.Features.Automations;

namespace MyWorkHub.Infrastructure.Features.Automations.Persistence;

/// <summary>
/// JSON payloads stored in text columns: a run's per-step results (<c>AutomationRuns.Details</c>) and a
/// plan's day numbers (<c>RemoteWorkSchedules.DaysJson</c>). Source-generated, so no reflection-based
/// serialization. Unreadable payloads (hand-edited or from an older schema) read back as empty rather
/// than breaking the page that lists them.
/// </summary>
internal static class AutomationJson
{
    public static string SerializeSteps(IReadOnlyList<AutomationStepResult> steps)
        => JsonSerializer.Serialize([.. steps], AutomationJsonContext.Default.ListAutomationStepResult);

    public static IReadOnlyList<AutomationStepResult> DeserializeSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, AutomationJsonContext.Default.ListAutomationStepResult) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeDays(IEnumerable<int> days)
        => JsonSerializer.Serialize([.. days], AutomationJsonContext.Default.ListInt32);

    public static IReadOnlyList<int> DeserializeDays(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize(json, AutomationJsonContext.Default.ListInt32) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<AutomationStepResult>))]
[JsonSerializable(typeof(List<int>))]
internal sealed partial class AutomationJsonContext : JsonSerializerContext;
