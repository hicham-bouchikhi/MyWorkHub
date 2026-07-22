using MyWorkHub.Infrastructure.Graph;

namespace MyWorkHub.Infrastructure.AzureDevOps;

/// <summary>Detects @mentions of a given display name inside AzDO comment HTML.</summary>
public static class WorkItemMentionScanner
{
    private const int DEFAULT_SNIPPET_LENGTH = 150;

    /// <summary>
    /// Returns true if the comment HTML contains <c>@{displayName}</c> (case-insensitive,
    /// full-name match — partial matches such as <c>@Hicham</c> alone are rejected).
    /// </summary>
    public static bool ContainsMentionOf(string? html, string? displayName)
    {
        if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        var text = HtmlBodyExtractor.StripTags(html);
        return text.Contains("@" + displayName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a plain-text preview of the comment, trimmed to <paramref name="maxLength"/>
    /// characters. Appends "…" when truncated.
    /// </summary>
    public static string ExtractSnippet(string? html, int maxLength = DEFAULT_SNIPPET_LENGTH)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = HtmlBodyExtractor.StripTags(html).Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength].TrimEnd() + "…";
    }
}
