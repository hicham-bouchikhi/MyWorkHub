using MyWorkHub.Infrastructure.Features.AzureDevOps;

namespace MyWorkHub.Infrastructure.Tests.Features.AzureDevOps;

public sealed class WorkItemMentionScannerTests
{
    private const string NAME = "Jane Doe";

    [Theory]
    [InlineData("<div><a href=\"#\" data-vss-mention=\"version:2.0,abc\">@Jane Doe</a> please review</div>")]
    [InlineData("<p>ping @jane doe</p>")]
    [InlineData("@Jane Doe")]
    [InlineData("<p>Thanks @Jane&nbsp;Doe, merged.</p>")]
    [InlineData("<p>cc @Jane Doe.</p>")]
    public void Should_find_a_whole_name_mention_case_insensitively(string html)
    {
        Assert.True(WorkItemMentionScanner.ContainsMentionOf(html, NAME));
    }

    [Theory]
    [InlineData("<p>Jane Doe said hi</p>")] // no @
    [InlineData("<p>@Jane</p>")] // first name only
    [InlineData("<p>@Jane Doerty</p>")] // someone else whose name starts the same
    [InlineData("<p>@Jane Doe2</p>")]
    [InlineData("")]
    [InlineData(null)]
    public void Should_not_report_a_mention_that_is_not_the_users(string? html)
    {
        Assert.False(WorkItemMentionScanner.ContainsMentionOf(html, NAME));
    }

    [Fact]
    public void Should_find_a_later_whole_mention_after_a_partial_one()
    {
        Assert.True(WorkItemMentionScanner.ContainsMentionOf("@Jane Doerty and @Jane Doe", NAME));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_never_match_a_blank_display_name(string? displayName)
    {
        Assert.False(WorkItemMentionScanner.ContainsMentionOf("<p>@ anything</p>", displayName));
    }

    [Fact]
    public void Should_strip_markup_scripts_and_entities_into_single_spaced_text()
    {
        var text = WorkItemMentionScanner.StripTags(
            "<style>p{color:red}</style><p>Fish &amp; chips</p>\n\n<script>alert(1)</script><div>&lt;ok&gt;</div>");

        Assert.Equal("Fish & chips <ok>", text);
    }

    [Fact]
    public void Should_keep_a_short_snippet_whole()
    {
        Assert.Equal("@Jane Doe hi", WorkItemMentionScanner.ExtractSnippet("<p>@Jane Doe <b>hi</b></p>"));
    }

    [Fact]
    public void Should_cut_a_long_snippet_with_an_ellipsis()
    {
        var snippet = WorkItemMentionScanner.ExtractSnippet("<p>" + new string('a', 20) + "</p>", maxLength: 10);

        Assert.Equal(new string('a', 10) + "…", snippet);
    }

    [Fact]
    public void Should_return_an_empty_snippet_for_an_empty_comment()
    {
        Assert.Equal("", WorkItemMentionScanner.ExtractSnippet(null));
    }
}
