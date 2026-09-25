using System.Net;
using System.Text.RegularExpressions;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>
/// Finds @mentions of a user in Azure DevOps comment HTML. Azure DevOps renders a mention as
/// <c>&lt;a href="#" data-vss-mention="…"&gt;@Display Name&lt;/a&gt;</c>, so after stripping the markup a
/// mention reads as <c>@Display Name</c> in the text.
/// </summary>
internal static partial class WorkItemMentionScanner
{
    public const int DEFAULT_SNIPPET_LENGTH = 150;

    private const string ELLIPSIS = "…";

    [GeneratedRegex(@"<(style|script)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleOrScriptBlock();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>
    /// True when the comment's text contains <c>@{displayName}</c> (case-insensitive) as a whole name: the
    /// mention must not continue with a letter or digit, so <c>@Jane Doe</c> is not found in
    /// <c>@Jane Doerty</c>.
    /// </summary>
    public static bool ContainsMentionOf(string? html, string? displayName)
    {
        if (string.IsNullOrEmpty(html) || string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var text = StripTags(html);
        var mention = "@" + Whitespace().Replace(displayName.Trim(), " ");

        for (var start = text.IndexOf(mention, StringComparison.OrdinalIgnoreCase);
             start >= 0;
             start = text.IndexOf(mention, start + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = start + mention.Length;
            if (end == text.Length || !char.IsLetterOrDigit(text[end]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The comment as plain text, cut to <paramref name="maxLength"/> characters with an ellipsis when longer.</summary>
    public static string ExtractSnippet(string? html, int maxLength = DEFAULT_SNIPPET_LENGTH)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var text = StripTags(html);
        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + ELLIPSIS;
    }

    /// <summary>Basic HTML → text: drops style/script blocks and tags, decodes entities, collapses whitespace.</summary>
    public static string StripTags(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        var text = StyleOrScriptBlock().Replace(html, " ");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        return Whitespace().Replace(text, " ").Trim();
    }
}
