using MyWorkHub.UI.Features.Email;

namespace MyWorkHub.UI.Tests.Features.Email;

public sealed class ReaderNavigationGuardTests
{
    private readonly ReaderNavigationGuard _guard = new();

    [Theory]
    [InlineData("about:blank")]
    [InlineData("data:text/html;base64,PHA+")]
    public void Should_allow_the_document_the_view_just_handed_over(string reported)
    {
        _guard.ExpectDocument();

        Assert.Equal(ReaderNavigation.ALLOW, _guard.Decide(new Uri(reported)));
    }

    [Fact]
    public void Should_allow_our_document_when_the_platform_reports_no_url()
    {
        _guard.ExpectDocument();

        Assert.Equal(ReaderNavigation.ALLOW, _guard.Decide(null));
    }

    [Fact]
    public void Should_block_a_data_url_followed_from_inside_the_message()
    {
        _guard.ExpectDocument();
        _guard.Decide(new Uri("about:blank"));

        Assert.Equal(ReaderNavigation.BLOCK, _guard.Decide(new Uri("data:text/html,<script>alert(1)</script>")));
    }

    [Fact]
    public void Should_allow_our_document_only_once()
    {
        _guard.ExpectDocument();
        _guard.Decide(new Uri("data:text/html,ours"));

        Assert.Equal(ReaderNavigation.BLOCK, _guard.Decide(new Uri("data:text/html,theirs")));
    }

    [Fact]
    public void Should_keep_in_page_anchors_inside_the_document()
    {
        Assert.Equal(ReaderNavigation.ALLOW, _guard.Decide(new Uri("about:blank#section-2")));
    }

    [Theory]
    [InlineData("https://dev.azure.com/cegid/Retail")]
    [InlineData("http://example.com")]
    [InlineData("mailto:someone@cegid.com")]
    public void Should_send_web_and_mail_links_to_the_browser(string url)
    {
        Assert.Equal(ReaderNavigation.OPEN_EXTERNALLY, _guard.Decide(new Uri(url)));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-settings:privacy")]
    public void Should_block_every_other_scheme(string url)
    {
        Assert.Equal(ReaderNavigation.BLOCK, _guard.Decide(new Uri(url)));
    }
}
