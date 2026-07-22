using MyWorkHub.Infrastructure.Graph;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class GraphEmailQueryTests
{
    [Fact]
    public void Watched_folder_ids_returns_the_configured_folders_when_set()
    {
        string[] configured = ["inbox-id", "clients-id", "proj-id"];

        var result = GraphEmailQuery.WatchedFolderIds(configured);

        Assert.Equal(configured, result);
    }

    [Fact]
    public void Watched_folder_ids_falls_back_to_inbox_when_none_configured()
    {
        var result = GraphEmailQuery.WatchedFolderIds([]);

        Assert.Equal(["inbox"], result);
    }
}
