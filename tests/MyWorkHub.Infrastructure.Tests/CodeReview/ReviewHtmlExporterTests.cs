using MyWorkHub.Core.Models;
using MyWorkHub.Infrastructure.CodeReview;

namespace MyWorkHub.Infrastructure.Tests.CodeReview;

public sealed class ReviewHtmlExporterTests
{
    private static PullRequestItem Pr() =>
        new(42, "Fix the bug", "Jane", "MyRepo", DateTime.UtcNow, "No vote", "https://example/42")
        {
            SourceBranch = "feature/x",
            TargetBranch = "main",
        };

    [Fact]
    public void Export_writes_a_self_contained_html_report_with_the_rendered_markdown()
    {
        const string markdown = """
            ## Summary
            A **small** change.

            ## Issues Found
            | Severity | File | Line | Issue |
            |----------|------|------|-------|
            | High | a.cs | 10 | Null deref |
            """;

        var path = ReviewHtmlExporter.Export(Pr(), markdown);
        try
        {
            Assert.True(File.Exists(path));
            var html = File.ReadAllText(path);

            Assert.Contains("<!DOCTYPE html>", html, StringComparison.Ordinal);
            Assert.Contains("<style>", html, StringComparison.Ordinal);      // inlined, self-contained
            Assert.Contains("<table", html, StringComparison.Ordinal);       // markdown table rendered
            Assert.Contains("<strong>small</strong>", html, StringComparison.Ordinal);
            Assert.Contains("MyRepo PR #42", html, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
