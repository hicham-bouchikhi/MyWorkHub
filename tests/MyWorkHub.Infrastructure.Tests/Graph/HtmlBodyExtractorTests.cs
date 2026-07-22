using MyWorkHub.Infrastructure.Graph;

namespace MyWorkHub.Infrastructure.Tests.Graph;

public sealed class HtmlBodyExtractorTests
{
    [Fact]
    public void StripTags_removes_html_markup_and_preserves_text()
    {
        var result = HtmlBodyExtractor.StripTags("<p>Hello <b>world</b>!</p>");

        Assert.Equal("Hello world !", result);
    }

    [Fact]
    public void StripTags_decodes_common_html_entities()
    {
        var result = HtmlBodyExtractor.StripTags("A &amp; B &lt;tag&gt; &quot;quoted&quot; &nbsp;space");

        Assert.Contains("A & B", result, StringComparison.Ordinal);
        Assert.Contains("<tag>", result, StringComparison.Ordinal);
        Assert.Contains("\"quoted\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StripTags_removes_style_and_script_block_contents_entirely()
    {
        const string html = "<html><head><style>body { color: red; }</style></head>" +
                            "<body><script>alert('xss');</script><p>Real content</p></body></html>";

        var result = HtmlBodyExtractor.StripTags(html);

        Assert.Contains("Real content", result, StringComparison.Ordinal);
        Assert.DoesNotContain("color: red", result, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StripTags_collapses_whitespace_into_single_spaces()
    {
        var result = HtmlBodyExtractor.StripTags("<p>line one</p>\n\n<p>line two</p>");

        Assert.DoesNotContain("\n", result, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StripTags_on_empty_or_null_input_returns_empty_string(string? input)
    {
        var result = HtmlBodyExtractor.StripTags(input!);

        Assert.Equal(string.Empty, result);
    }
}
