using System.Globalization;
using System.Net;
using System.Text;
using MyWorkHub.Core;
using MyWorkHub.Core.Models;
using Markdig;

namespace MyWorkHub.Infrastructure.CodeReview;

/// <summary>
/// Renders a Claude Code review (Markdown) into a self-contained HTML report written under
/// <see cref="AppPaths.ReviewsDir"/>. The stylesheet is inlined so the file opens standalone
/// in any browser with no external dependencies.
/// </summary>
public static class ReviewHtmlExporter
{
    private static readonly MarkdownPipeline _pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>Writes the report and returns its full path.</summary>
    public static string Export(PullRequestItem pr, string markdown)
    {
        ArgumentNullException.ThrowIfNull(pr);
        ArgumentNullException.ThrowIfNull(markdown);

        var body = Markdown.ToHtml(markdown, _pipeline);
        var title = WebUtility.HtmlEncode($"{pr.Repository} PR #{pr.Id}: {pr.Title}");
        var subtitle = WebUtility.HtmlEncode(
            $"{pr.Author} · {pr.SourceBranch} → {pr.TargetBranch}");

        var html = BuildDocument(title, subtitle, body);

        Directory.CreateDirectory(AppPaths.ReviewsDir);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var fileName = $"{Sanitize(pr.Repository)}-PR{pr.Id}-{stamp}.html";
        var path = Path.Combine(AppPaths.ReviewsDir, fileName);

        File.WriteAllText(path, html, Encoding.UTF8);
        return path;
    }

    private static string BuildDocument(string title, string subtitle, string body)
        => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{{title}}</title>
          <style>
            :root { color-scheme: light dark; }
            body {
              font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
              line-height: 1.55; max-width: 900px; margin: 0 auto; padding: 2rem 1.5rem;
              color: #1f2328; background: #ffffff;
            }
            header { border-bottom: 1px solid #d0d7de; padding-bottom: 1rem; margin-bottom: 1.5rem; }
            header h1 { margin: 0 0 .25rem; font-size: 1.4rem; }
            header .sub { color: #57606a; font-size: .95rem; }
            h2 { border-bottom: 1px solid #d0d7de; padding-bottom: .3rem; margin-top: 2rem; }
            table { border-collapse: collapse; width: 100%; margin: 1rem 0; }
            th, td { border: 1px solid #d0d7de; padding: .5rem .75rem; text-align: left; vertical-align: top; }
            th { background: #f6f8fa; }
            code { background: #f6f8fa; padding: .15em .35em; border-radius: 4px; font-size: .9em;
              font-family: ui-monospace, "Cascadia Code", "Consolas", monospace; }
            pre { background: #f6f8fa; padding: 1rem; border-radius: 6px; overflow-x: auto; }
            pre code { background: none; padding: 0; }
            blockquote { margin: 0; padding: 0 1rem; color: #57606a; border-left: .25rem solid #d0d7de; }
            @media (prefers-color-scheme: dark) {
              body { color: #e6edf3; background: #0d1117; }
              header, h2 { border-color: #30363d; }
              header .sub, blockquote { color: #8b949e; }
              th, td { border-color: #30363d; }
              th, code, pre { background: #161b22; }
            }
          </style>
        </head>
        <body>
          <header>
            <h1>{{title}}</h1>
            <div class="sub">{{subtitle}}</div>
          </header>
          {{body}}
        </body>
        </html>
        """;

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        return builder.Length == 0 ? "repo" : builder.ToString();
    }
}
