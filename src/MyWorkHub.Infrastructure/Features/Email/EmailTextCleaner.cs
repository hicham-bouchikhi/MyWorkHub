using System.Text.RegularExpressions;

namespace MyWorkHub.Infrastructure.Features.Email;

/// <summary>
/// Tidies Outlook's HTML-to-text conversion for display: it renders every link as <c>text &lt;url&gt;</c>
/// (Safe Links URLs run to hundreds of characters) and every image as <c>[url]</c>.
/// </summary>
internal static partial class EmailTextCleaner
{
    public static string CleanBody(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var cleaned = StripLinks(text).Replace("\r\n", "\n", StringComparison.Ordinal);
        cleaned = TrailingSpaces().Replace(cleaned, "");
        return BlankLineRuns().Replace(cleaned, "\n\n").Trim();
    }

    public static string CleanPreview(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Whitespace().Replace(StripLinks(text), " ").Trim();
    }

    private static string StripLinks(string text)
        => ImagePlaceholder().Replace(LinkTarget().Replace(text, ""), "");

    [GeneratedRegex(@"[ \t]*<(?:https?|mailto):[^>\s]*>")]
    private static partial Regex LinkTarget();

    [GeneratedRegex(@"\[https?://[^\]\s]*\][ \t]*")]
    private static partial Regex ImagePlaceholder();

    [GeneratedRegex(@"[ \t]+$", RegexOptions.Multiline)]
    private static partial Regex TrailingSpaces();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLineRuns();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
