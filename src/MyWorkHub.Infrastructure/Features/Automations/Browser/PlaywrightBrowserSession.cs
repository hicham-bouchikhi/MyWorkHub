using Microsoft.Playwright;

namespace MyWorkHub.Infrastructure.Features.Automations.Browser;

/// <summary>
/// <see cref="IBrowserSession"/> over one Playwright <see cref="IPage"/>. Playwright's .NET API takes no
/// <see cref="CancellationToken"/>, so each call checks the token first and stops waiting (via
/// <see cref="Task.WaitAsync(CancellationToken)"/>) when it fires; the abandoned browser work ends when the
/// session is disposed, which closes the browser.
/// </summary>
internal sealed class PlaywrightBrowserSession : IBrowserSession
{
    private const string FALLBACK_FILE_NAME = "download";

    private readonly IPage _page;
    private readonly Func<ValueTask> _closeBrowser;
    private bool _disposed;

    /// <param name="page">The page every action runs on.</param>
    /// <param name="closeBrowser">Closes the browser (and the Playwright driver) that owns the page.</param>
    public PlaywrightBrowserSession(IPage page, Func<ValueTask> closeBrowser)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(closeBrowser);
        _page = page;
        _closeBrowser = closeBrowser;
    }

    public async Task GoToAsync(Uri url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ct.ThrowIfCancellationRequested();
        await _page.GotoAsync(url.AbsoluteUri).WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task FillAsync(string selector, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(value);
        ct.ThrowIfCancellationRequested();
        await _page.FillAsync(selector, value).WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ct.ThrowIfCancellationRequested();
        await _page.ClickAsync(selector).WaitAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> DownloadFileAsync(string triggerSelector, string destinationDirectory, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(destinationDirectory);

        // Start waiting for the download BEFORE clicking. The download event can fire while the click is
        // still being acknowledged; a waiter registered after the click would miss it and hang until the
        // timeout.
        var pendingDownload = _page.WaitForDownloadAsync();
        IDownload download;
        try
        {
            await _page.ClickAsync(triggerSelector).WaitAsync(ct).ConfigureAwait(false);
            download = await pendingDownload.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // The waiter is abandoned (it will time out or fault when the browser closes): observe it so
            // its failure is not reported as an unobserved task exception.
            _ = pendingDownload.ContinueWith(
                static t => t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }

        // Written straight to disk by the browser driver; the content never passes through managed memory.
        var path = UniquePath(destinationDirectory, SafeFileName(download.SuggestedFilename));
        await download.SaveAsAsync(path).WaitAsync(ct).ConfigureAwait(false);
        return path;
    }

    public async Task CaptureScreenshotAsync(string destinationPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ct.ThrowIfCancellationRequested();

        if (Path.GetDirectoryName(destinationPath) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = destinationPath, FullPage = true })
            .WaitAsync(ct)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _closeBrowser().ConfigureAwait(false);
    }

    // The suggested name comes from the remote site: keep only a bare file name (no directory parts,
    // no characters the file system rejects), so a hostile name cannot write outside the target folder.
    internal static string SafeFileName(string? suggested)
    {
        var name = Path.GetFileName((suggested ?? "").Replace('\\', '/'));
        var invalid = Path.GetInvalidFileNameChars();
        name = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().TrimStart('.');
        return name.Length == 0 ? FALLBACK_FILE_NAME : name;
    }

    // "attestation.pdf" → "attestation (2).pdf" when the name is already taken.
    internal static string UniquePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(directory, $"{stem} ({n}){extension}");
        }

        return path;
    }
}
