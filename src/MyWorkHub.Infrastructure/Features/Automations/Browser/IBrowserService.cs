namespace MyWorkHub.Infrastructure.Features.Automations.Browser;

/// <summary>
/// Launches isolated, automatable browser sessions for the site automations (Playwright, Chromium only).
/// Not to be confused with <c>IBrowserLauncher</c>, the UI-layer service that opens a URL in the user's
/// default browser.
/// </summary>
internal interface IBrowserService
{
    /// <summary>A fresh browser with an empty profile (no cookies shared with other sessions). Dispose it when done.</summary>
    Task<IBrowserSession> OpenSessionAsync(CancellationToken ct = default);
}

/// <summary>
/// One page of an automated browser. Selectors are Playwright selectors (CSS, <c>text=…</c>, <c>role=…</c>);
/// every action waits for its element up to the configured timeout.
/// </summary>
internal interface IBrowserSession : IAsyncDisposable
{
    /// <summary>Navigates to <paramref name="url"/> and waits for the page to load.</summary>
    Task GoToAsync(Uri url, CancellationToken ct = default);

    /// <summary>Types <paramref name="value"/> into the matching input, replacing its content.</summary>
    Task FillAsync(string selector, string value, CancellationToken ct = default);

    /// <summary>Clicks the matching element.</summary>
    Task ClickAsync(string selector, CancellationToken ct = default);

    /// <summary>
    /// Clicks the element that triggers a file download and saves the file into
    /// <paramref name="destinationDirectory"/> under the name the site suggested (made unique if taken).
    /// Returns the saved file's path — the content is never loaded into memory.
    /// </summary>
    Task<string> DownloadFileAsync(string triggerSelector, string destinationDirectory, CancellationToken ct = default);

    /// <summary>Saves a full-page PNG screenshot to <paramref name="destinationPath"/> (for diagnosing a failed run).</summary>
    Task CaptureScreenshotAsync(string destinationPath, CancellationToken ct = default);
}
