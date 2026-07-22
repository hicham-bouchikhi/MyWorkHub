using MyWorkHub.Infrastructure.AzureDevOps;

namespace MyWorkHub.Infrastructure.Tests.AzureDevOps;

public sealed class WorkItemMentionScannerTests
{
    [Fact]
    public void Should_detect_mention_in_plain_at_symbol_html()
    {
        const string html = "<div>Hey @Hicham BOUCHIKHI please review this.</div>";
        Assert.True(WorkItemMentionScanner.ContainsMentionOf(html, "Hicham BOUCHIKHI"));
    }

    [Fact]
    public void Should_detect_mention_in_azdo_vss_mention_anchor()
    {
        const string html = """<div><a href="#" data-vss-mention="version:2.0,abc">@Hicham BOUCHIKHI</a> can you check?</div>""";
        Assert.True(WorkItemMentionScanner.ContainsMentionOf(html, "Hicham BOUCHIKHI"));
    }

    [Fact]
    public void Should_match_case_insensitively()
    {
        const string html = "<div>@hicham bouchikhi</div>";
        Assert.True(WorkItemMentionScanner.ContainsMentionOf(html, "Hicham BOUCHIKHI"));
    }

    [Fact]
    public void Should_return_false_when_name_not_present()
    {
        const string html = "<div>@Alice please check</div>";
        Assert.False(WorkItemMentionScanner.ContainsMentionOf(html, "Hicham BOUCHIKHI"));
    }

    [Theory]
    [InlineData(null, "Hicham BOUCHIKHI")]
    [InlineData("", "Hicham BOUCHIKHI")]
    [InlineData("<div>@Hicham BOUCHIKHI</div>", null)]
    [InlineData("<div>@Hicham BOUCHIKHI</div>", "")]
    public void Should_return_false_for_null_or_empty_inputs(string? html, string? name)
    {
        Assert.False(WorkItemMentionScanner.ContainsMentionOf(html, name!));
    }

    [Fact]
    public void Should_not_false_positive_on_partial_name_match()
    {
        const string html = "<div>@Hicham</div>";
        Assert.False(WorkItemMentionScanner.ContainsMentionOf(html, "Hicham BOUCHIKHI"));
    }

    [Fact]
    public void Should_extract_snippet_as_plain_text()
    {
        const string html = "<div><b>Hello</b> @Hicham BOUCHIKHI can you look at this?</div>";
        var snippet = WorkItemMentionScanner.ExtractSnippet(html);
        Assert.DoesNotContain("<", snippet, StringComparison.Ordinal);
        Assert.Contains("Hello", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_truncate_snippet_at_max_length()
    {
        var longText = new string('x', 200);
        var html = $"<div>{longText}</div>";
        var snippet = WorkItemMentionScanner.ExtractSnippet(html, 50);
        Assert.True(snippet.Length <= 55); // 50 chars + possible "…"
        Assert.EndsWith("…", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_return_empty_snippet_for_null_html()
    {
        Assert.Equal(string.Empty, WorkItemMentionScanner.ExtractSnippet(null));
    }
}
