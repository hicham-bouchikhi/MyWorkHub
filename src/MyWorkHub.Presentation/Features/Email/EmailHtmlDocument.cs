using System.Text.RegularExpressions;

namespace MyWorkHub.Presentation.Features.Email;

/// <summary>
/// Wraps a message's (untrusted) HTML into the document the reading pane renders. The leading
/// Content-Security-Policy is the sandbox: no scripts, frames, plugins, forms or remote fetches —
/// only inline styles and <c>data:</c> images, plus http(s) images once the user opts in for that
/// message (remote images are how senders track opens). It is emitted first so the parser applies it
/// before any of the sender's markup; a policy the sender adds can only tighten it further.
/// </summary>
public static partial class EmailHtmlDocument
{
    private const string BLOCKED_IMAGES = "img-src data:;";
    private const string ALLOWED_IMAGES = "img-src data: https: http:;";

    // Emails are authored for a white page; a light color scheme keeps the WebView from inverting form controls.
    private const string BASE_STYLE =
        "<meta name=\"color-scheme\" content=\"light\">" +
        "<style>body{margin:0;padding:12px;background:#fff;color:#1f2328;" +
        "font-family:'Segoe UI',system-ui,sans-serif;font-size:14px;overflow-wrap:anywhere}img{max-width:100%;height:auto}</style>";

    /// <summary>The full document for <paramref name="html"/>; remote images load only when <paramref name="allowRemoteImages"/>.</summary>
    public static string Build(string html, bool allowRemoteImages)
    {
        ArgumentNullException.ThrowIfNull(html);

        var policy = "default-src 'none'; style-src 'unsafe-inline'; font-src data:; " +
                     (allowRemoteImages ? ALLOWED_IMAGES : BLOCKED_IMAGES) +
                     " form-action 'none'; base-uri 'none'";
        return $"<meta http-equiv=\"Content-Security-Policy\" content=\"{policy}\">" +
               BASE_STYLE +
               MetaRefresh().Replace(html, "");
    }

    /// <summary>Whether the message references images on a remote server (drives the "Load images" prompt).</summary>
    public static bool HasRemoteImages(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return RemoteImage().IsMatch(html);
    }

    [GeneratedRegex(@"<meta\b[^>]*http-equiv\s*=\s*[""']?\s*refresh[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaRefresh();

    [GeneratedRegex(@"\b(?:src|srcset|background)\s*=\s*[""']?\s*https?:|url\(\s*[""']?\s*https?:", RegexOptions.IgnoreCase)]
    private static partial Regex RemoteImage();
}
