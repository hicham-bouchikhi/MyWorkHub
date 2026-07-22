using MyWorkHub.Core.Models;
using MyWorkHub.Infrastructure.Graph;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class GraphEmailAggregatorTests
{
    private static EmailItem Item(string id, DateTime receivedAt) =>
        new(id, "Sender", "Subject", "Preview", receivedAt, IsFlagged: false);

    [Fact]
    public void Merges_folders_and_sorts_most_recent_first()
    {
        var older = Item("a", new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));
        var newer = Item("b", new DateTime(2026, 7, 5, 8, 0, 0, DateTimeKind.Utc));

        var merged = GraphEmailAggregator.Merge([[older], [newer]]);

        Assert.Equal(["b", "a"], merged.Select(m => m.Id));
    }

    [Fact]
    public void Deduplicates_by_id_when_a_message_appears_in_two_folders()
    {
        var received = new DateTime(2026, 7, 3, 8, 0, 0, DateTimeKind.Utc);
        var inInbox = Item("dup", received);
        var inOther = Item("dup", received);

        var merged = GraphEmailAggregator.Merge([[inInbox], [inOther]]);

        Assert.Single(merged);
        Assert.Equal("dup", merged[0].Id);
    }
}
