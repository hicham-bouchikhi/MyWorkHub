namespace MyWorkHub.UI.Features.Email;

/// <summary>What the reading pane does with a navigation it is about to perform.</summary>
internal enum ReaderNavigation
{
    /// <summary>Let the WebView load it (our own document, or an in-page anchor).</summary>
    ALLOW,

    /// <summary>Cancel it and hand the link to the default browser.</summary>
    OPEN_EXTERNALLY,

    /// <summary>Cancel it silently.</summary>
    BLOCK,
}

/// <summary>
/// Navigation policy of the email reading pane. The pane only ever shows the sandboxed document the view hands
/// it: that one load is allowed whatever URL the platform reports for it (<c>about:blank</c> on WebKitGTK, a
/// <c>data:</c> URL on WebView2), and is consumed — a later <c>data:</c> navigation (a link in the message) would
/// load a document <em>without</em> the CSP, so it is blocked. <c>about:</c> URLs (in-page anchors) stay inside the
/// document; web and mail links leave for the browser; every other scheme is blocked.
/// </summary>
internal sealed class ReaderNavigationGuard
{
    private bool _expectingDocument;

    /// <summary>Call right before handing the WebView a new document.</summary>
    public void ExpectDocument() => _expectingDocument = true;

    public ReaderNavigation Decide(Uri? request)
    {
        if (request is not { IsAbsoluteUri: true } || request.Scheme == "data")
        {
            var isOurs = _expectingDocument;
            _expectingDocument = false;
            return isOurs ? ReaderNavigation.ALLOW : ReaderNavigation.BLOCK;
        }

        if (request.Scheme == "about")
        {
            _expectingDocument = false;
            return ReaderNavigation.ALLOW;
        }

        return request.Scheme is "http" or "https" or "mailto" ? ReaderNavigation.OPEN_EXTERNALLY : ReaderNavigation.BLOCK;
    }
}
