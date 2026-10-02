using MyWorkHub.Infrastructure.Features.Email;

namespace MyWorkHub.Infrastructure.Tests.Features.Email;

public sealed class EmailTextCleanerTests
{
    [Fact]
    public void Should_drop_link_targets_when_the_body_has_text_followed_by_an_angle_bracket_url()
    {
        var body = "View changes <https://eur01.safelinks.protection.outlook.com/?url=https%3A%2F%2Fdev.azure.com&data=05>";

        Assert.Equal("View changes", EmailTextCleaner.CleanBody(body));
    }

    [Fact]
    public void Should_drop_image_placeholders_when_the_body_has_bracketed_urls()
    {
        var body = "[https://cdn.vsassets.io/content/notifications/v3/file.png]  /src/Consts.cs";

        Assert.Equal("/src/Consts.cs", EmailTextCleaner.CleanBody(body));
    }

    [Fact]
    public void Should_keep_a_bare_url_when_it_is_the_only_text()
    {
        Assert.Equal("See https://example.com/a", EmailTextCleaner.CleanBody("See https://example.com/a"));
    }

    [Fact]
    public void Should_collapse_blank_line_runs_and_trailing_spaces_when_cleaning_the_body()
    {
        Assert.Equal("a\n\nb", EmailTextCleaner.CleanBody("a   \r\n\r\n\r\n\r\nb\r\n"));
    }

    [Fact]
    public void Should_flatten_to_one_line_when_cleaning_a_preview()
    {
        var preview = "Noureddine pushed\r\n  [Microsoft]   Azure DevOps\r\nView changes <https://x.example/y>";

        Assert.Equal("Noureddine pushed [Microsoft] Azure DevOps View changes", EmailTextCleaner.CleanPreview(preview));
    }
}
