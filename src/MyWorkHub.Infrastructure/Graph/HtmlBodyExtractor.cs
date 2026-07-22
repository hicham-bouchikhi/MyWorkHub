using System.Text.RegularExpressions;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>Strips HTML markup from a Graph message body, producing plain text suitable for AI prompts.</summary>
public static partial class HtmlBodyExtractor
{
    [GeneratedRegex(@"<(style|script)[^>]*>[\s\S]*?</(style|script)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleScriptRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = StyleScriptRegex().Replace(html, " ");
        text = TagRegex().Replace(text, " ");
        text = text
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase)
            .Replace("&lt;", "<", StringComparison.OrdinalIgnoreCase)
            .Replace("&gt;", ">", StringComparison.OrdinalIgnoreCase)
            .Replace("&quot;", "\"", StringComparison.OrdinalIgnoreCase)
            .Replace("&apos;", "'", StringComparison.OrdinalIgnoreCase)
            .Replace("&#160;", " ", StringComparison.Ordinal);

        return WhitespaceRegex().Replace(text, " ").Trim();
    }
}
