using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Graph.Models;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Turns a Teams message body into a readable one-line preview. Teams message bodies are
/// usually HTML (<c>&lt;p&gt;</c>, <c>&lt;at&gt;</c> mentions, <c>&amp;nbsp;</c>, …), which
/// would otherwise leak markup into the digest. Plain-text bodies are returned untouched.
/// </summary>
internal static partial class TeamsMessagePreview
{
    public static string ToPlainText(string? content, BodyType? contentType)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "";
        }

        if (contentType != BodyType.Html)
        {
            return content.Trim();
        }

        // Block-level / break tags become spaces so words don't run together…
        var text = LineBreakTags().Replace(content, " ");
        // …then strip every remaining tag (keeping inner text, e.g. <at>Name</at> -> Name)…
        text = Tags().Replace(text, "");
        // …decode entities (&amp; &nbsp; &lt; …)…
        text = WebUtility.HtmlDecode(text);
        // …and collapse the whitespace the markup left behind.
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|/h[1-6]|/tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTags();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
