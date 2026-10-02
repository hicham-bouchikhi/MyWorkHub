using MyWorkHub.Presentation.Features.Email;

namespace MyWorkHub.UI.Tests.Features.Email;

public sealed class EmailHtmlDocumentTests
{
    [Fact]
    public void Should_open_with_a_policy_that_forbids_scripts_and_remote_loads_when_images_are_blocked()
    {
        var document = EmailHtmlDocument.Build("<p>Hi</p>", allowRemoteImages: false);

        Assert.StartsWith("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none';", document, StringComparison.Ordinal);
        Assert.Contains("img-src data:;", document, StringComparison.Ordinal);
        Assert.DoesNotContain("script-src", document, StringComparison.Ordinal);
        Assert.EndsWith("<p>Hi</p>", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_allow_remote_images_only_when_asked()
    {
        var document = EmailHtmlDocument.Build("<p>Hi</p>", allowRemoteImages: true);

        Assert.Contains("img-src data: https: http:;", document, StringComparison.Ordinal);
    }

    [Fact]
    public void Should_strip_meta_refresh_so_the_message_cannot_navigate_by_itself()
    {
        var document = EmailHtmlDocument.Build("<META HTTP-EQUIV='refresh' content='0;url=https://evil.example'><p>x</p>", allowRemoteImages: false);

        Assert.DoesNotContain("evil.example", document, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<img src=\"https://cdn.example/logo.png\">", true)]
    [InlineData("<td background='http://cdn.example/bg.jpg'>", true)]
    [InlineData("<div style=\"background:url( https://cdn.example/a.png)\">", true)]
    [InlineData("<img src=\"data:image/png;base64,AQID\">", false)]
    [InlineData("<a href=\"https://example.com\">link</a>", false)]
    public void Should_detect_remote_images(string html, bool expected)
    {
        Assert.Equal(expected, EmailHtmlDocument.HasRemoteImages(html));
    }
}
