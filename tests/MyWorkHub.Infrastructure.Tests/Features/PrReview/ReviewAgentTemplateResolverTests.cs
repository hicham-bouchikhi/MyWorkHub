using MyWorkHub.Infrastructure.Features.PrReview;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.PrReview;

public sealed class ReviewAgentTemplateResolverTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("review-agent-test-");
    private readonly RecordingProgress _progress = new();

    public void Dispose() => _directory.Delete(recursive: true);

    private string File(string name, string content)
    {
        var path = Path.Combine(_directory.FullName, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    private string Missing(string name) => Path.Combine(_directory.FullName, name);

    [Fact]
    public void Should_prefer_the_per_review_override_over_everything_else()
    {
        var resolver = new ReviewAgentTemplateResolver(File("configured.md", "CONFIGURED"), File("seeded.md", "SEEDED"));

        var template = resolver.Resolve(File("override.md", "OVERRIDE"), _progress);

        Assert.Equal("OVERRIDE", template);
        Assert.Contains(_progress.Messages, m => m.Contains("per-review", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_use_the_configured_template_when_there_is_no_override()
    {
        var resolver = new ReviewAgentTemplateResolver(File("configured.md", "CONFIGURED"), File("seeded.md", "SEEDED"));

        Assert.Equal("CONFIGURED", resolver.Resolve(overridePath: null, _progress));
        Assert.DoesNotContain(_progress.Messages, m => m.StartsWith("Warning", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_use_the_seeded_default_when_nothing_is_configured()
    {
        var resolver = new ReviewAgentTemplateResolver(configuredPath: null, File("seeded.md", "SEEDED"));

        Assert.Equal("SEEDED", resolver.Resolve(overridePath: "  ", _progress));
        Assert.Contains(_progress.Messages, m => m.Contains("built-in", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_use_the_compiled_in_default_when_the_seeded_copy_is_missing()
    {
        var resolver = new ReviewAgentTemplateResolver(configuredPath: null, Missing("seeded.md"));

        Assert.Equal(ReviewAgentTemplateResolver.DEFAULT_AGENT_TEMPLATE, resolver.Resolve(overridePath: null, _progress));
        Assert.DoesNotContain(_progress.Messages, m => m.StartsWith("Warning", StringComparison.Ordinal));
    }

    [Fact]
    public void Should_warn_and_fall_through_every_level_when_the_user_paths_do_not_resolve()
    {
        var resolver = new ReviewAgentTemplateResolver(Missing("configured.md"), Missing("seeded.md"));

        var template = resolver.Resolve(Missing("override.md"), _progress);

        Assert.Equal(ReviewAgentTemplateResolver.DEFAULT_AGENT_TEMPLATE, template);
        Assert.Collection(_progress.Messages,
            m => Assert.StartsWith("Warning: the per-review review agent", m, StringComparison.Ordinal),
            m => Assert.StartsWith("Warning: the configured review agent", m, StringComparison.Ordinal),
            m => Assert.Contains("compiled-in", m, StringComparison.Ordinal));
    }

    [Fact]
    public void Should_warn_and_fall_back_to_the_configured_template_when_the_override_is_missing()
    {
        var resolver = new ReviewAgentTemplateResolver(File("configured.md", "CONFIGURED"), File("seeded.md", "SEEDED"));

        Assert.Equal("CONFIGURED", resolver.Resolve(Missing("typo.md"), _progress));
        Assert.StartsWith("Warning: the per-review review agent", _progress.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Should_treat_an_empty_or_unreadable_configured_template_as_unresolved()
    {
        // An empty file and a directory path (unreadable as a file) both fall through to the seeded default.
        var emptyResolver = new ReviewAgentTemplateResolver(File("empty.md", "  \n"), File("seeded.md", "SEEDED"));
        var directoryResolver = new ReviewAgentTemplateResolver(_directory.FullName, File("seeded2.md", "SEEDED"));

        Assert.Equal("SEEDED", emptyResolver.Resolve(overridePath: null, _progress));
        Assert.Equal("SEEDED", directoryResolver.Resolve(overridePath: null, _progress));
        Assert.Equal(2, _progress.Messages.Count(m => m.StartsWith("Warning: the configured", StringComparison.Ordinal)));
    }

    [Fact]
    public void Should_ship_a_fallback_that_asks_for_the_report_sections()
    {
        Assert.Contains("## Issues Found", ReviewAgentTemplateResolver.DEFAULT_AGENT_TEMPLATE, StringComparison.Ordinal);
        Assert.Contains("## Verdict", ReviewAgentTemplateResolver.DEFAULT_AGENT_TEMPLATE, StringComparison.Ordinal);
    }
}
