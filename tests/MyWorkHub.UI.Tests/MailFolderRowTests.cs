using MyWorkHub.Core.Models;
using MyWorkHub.UI.ViewModels;

namespace MyWorkHub.UI.Tests;

/// <summary>
/// Tri-state tree behaviour for the Settings mail-folder picker. A parent row is
/// checked (true) when every descendant is watched, unchecked (false) when none is,
/// and indeterminate (null) when they are mixed — the "partial" state that still
/// counts the folder as watched.
/// </summary>
public sealed class MailFolderRowTests
{
    private static MailFolder Leaf(string id, bool watched) => new(id, id, 0, watched);

    private static IEnumerable<MailFolderRow> Flatten(MailFolderRow row)
    {
        yield return row;
        foreach (var child in row.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    [Fact]
    public void Checking_a_parent_watches_all_descendants()
    {
        var parent = new MailFolder("p", "Parent", 0, IsWatched: false)
        {
            Children =
            [
                Leaf("c1", false),
                new MailFolder("c2", "C2", 0, IsWatched: false) { Children = [Leaf("g", false)] },
            ],
        };
        var row = new MailFolderRow(parent);

        row.IsWatched = true;

        Assert.All(Flatten(row), r => Assert.True(r.IsWatched));
    }

    [Fact]
    public void Unchecking_a_parent_unwatches_all_descendants()
    {
        var parent = new MailFolder("p", "Parent", 0, IsWatched: true)
        {
            Children = [Leaf("c1", true), Leaf("c2", true)],
        };
        var row = new MailFolderRow(parent);

        row.IsWatched = false;

        Assert.All(Flatten(row), r => Assert.False(r.IsWatched));
    }

    [Fact]
    public void Parent_is_indeterminate_when_children_are_mixed()
    {
        var parent = new MailFolder("p", "Parent", 0, IsWatched: false)
        {
            Children = [Leaf("c1", false), Leaf("c2", false)],
        };
        var row = new MailFolderRow(parent);

        row.Children[0].IsWatched = true;

        Assert.Null(row.IsWatched);
    }

    [Fact]
    public void Parent_becomes_checked_when_all_children_are_watched()
    {
        var parent = new MailFolder("p", "Parent", 0, IsWatched: false)
        {
            Children = [Leaf("c1", false), Leaf("c2", false)],
        };
        var row = new MailFolderRow(parent);

        row.Children[0].IsWatched = true;
        row.Children[1].IsWatched = true;

        Assert.True(row.IsWatched);
    }

    [Fact]
    public void Parent_becomes_unchecked_when_all_children_are_unwatched()
    {
        var parent = new MailFolder("p", "Parent", 0, IsWatched: true)
        {
            Children = [Leaf("c1", true), Leaf("c2", true)],
        };
        var row = new MailFolderRow(parent);

        row.Children[0].IsWatched = false;
        row.Children[1].IsWatched = false;

        Assert.False(row.IsWatched);
    }

    [Fact]
    public void Indeterminate_state_bubbles_up_through_grandparents()
    {
        var root = new MailFolder("root", "Root", 0, IsWatched: false)
        {
            Children =
            [
                new MailFolder("mid", "Mid", 0, IsWatched: false)
                {
                    Children = [Leaf("a", false), Leaf("b", false)],
                },
            ],
        };
        var row = new MailFolderRow(root);

        // Tick one leaf two levels down.
        row.Children[0].Children[0].IsWatched = true;

        Assert.Null(row.Children[0].IsWatched); // mid: mixed
        Assert.Null(row.IsWatched);             // root: mixed
    }

    [Fact]
    public void Initial_state_is_derived_from_children_on_construction()
    {
        // Stored: parent unwatched, one child watched, one not → parent should load as indeterminate.
        var parent = new MailFolder("p", "Parent", 0, IsWatched: false)
        {
            Children = [Leaf("c1", true), Leaf("c2", false)],
        };

        var row = new MailFolderRow(parent);

        Assert.Null(row.IsWatched);
    }

    [Fact]
    public void Leaf_state_matches_its_stored_flag()
    {
        var watched = new MailFolderRow(Leaf("x", watched: true));
        var unwatched = new MailFolderRow(Leaf("y", watched: false));

        Assert.True(watched.IsWatched);
        Assert.False(unwatched.IsWatched);
    }
}
