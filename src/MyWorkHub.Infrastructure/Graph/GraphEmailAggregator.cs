using MyWorkHub.Core.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Merges the per-folder message pages returned by <see cref="GraphEmailService"/> into a
/// single digest: de-duplicated by message id (a message can surface in more than one folder
/// view) and sorted most-recent-first, honouring the "most recent first" contract on
/// <see cref="Core.Abstractions.IEmailService.GetImportantEmailsAsync"/>.
/// </summary>
public static class GraphEmailAggregator
{
    public static IReadOnlyList<EmailItem> Merge(IEnumerable<IReadOnlyList<EmailItem>> perFolder)
    {
        ArgumentNullException.ThrowIfNull(perFolder);

        return perFolder
            .SelectMany(static folder => folder)
            .DistinctBy(static m => m.Id, StringComparer.Ordinal)
            .OrderByDescending(static m => m.ReceivedAt)
            .ToList();
    }
}
