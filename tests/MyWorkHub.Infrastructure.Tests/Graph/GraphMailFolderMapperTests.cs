using MyWorkHub.Infrastructure.Graph;
using GraphFolder = Microsoft.Graph.Models.MailFolder;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class GraphMailFolderMapperTests
{
    [Fact]
    public void Maps_id_display_name_and_unread_count()
    {
        var folder = new GraphFolder { Id = "AAMk1", DisplayName = "Clients", UnreadItemCount = 3 };

        var mapped = GraphMailFolderMapper.ToMailFolder(folder, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("AAMk1", mapped.Id);
        Assert.Equal("Clients", mapped.DisplayName);
        Assert.Equal(3, mapped.UnreadItemCount);
        Assert.False(mapped.IsWatched);
    }

    [Fact]
    public void Is_watched_when_the_folder_id_is_in_the_watched_set()
    {
        var folder = new GraphFolder { Id = "AAMk1", DisplayName = "Clients" };

        var mapped = GraphMailFolderMapper.ToMailFolder(folder, new HashSet<string>(StringComparer.Ordinal) { "AAMk1" });

        Assert.True(mapped.IsWatched);
    }

    [Fact]
    public void Falls_back_to_a_placeholder_when_the_display_name_is_missing()
    {
        var folder = new GraphFolder { Id = "AAMk1" };

        var mapped = GraphMailFolderMapper.ToMailFolder(folder, new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("(unnamed folder)", mapped.DisplayName);
    }
}
