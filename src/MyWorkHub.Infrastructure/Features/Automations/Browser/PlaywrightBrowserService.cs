using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace MyWorkHub.Infrastructure.Features.Automations.Browser;

/// <summary>
/// Browser settings (configuration section <c>Automation:Browser</c>). Read key by key rather than through
/// the reflection-based configuration binder.
/// </summary>
/// <param name="Headless">Run without a visible window (default). Set to false to watch a run, e.g. when
/// working out a site's selectors.</param>
/// <param name="ActionTimeout">How long any single browser action may wait for its element or event.</param>
internal sealed record BrowserOptions(bool Headless, TimeSpan ActionTimeout)
{
    public const string SECTION = "Automation:Browser";

    private const int DEFAULT_TIMEOUT_SECONDS = 30;

    public static BrowserOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SECTION);
        var headless = !bool.TryParse(section[nameof(Headless)], out var parsedHeadless) || parsedHeadless;
        var timeoutSeconds = int.TryParse(section["TimeoutSeconds"], out var parsedTimeout) && parsedTimeout > 0
            ? parsedTimeout
            : DEFAULT_TIMEOUT_SECONDS;

        return new BrowserOptions(headless, TimeSpan.FromSeconds(timeoutSeconds));
    }
}

/// <summary>
/// <see cref="IBrowserService"/> over Playwright, Chromium only. The Chromium build matching the Playwright
/// package is installed on first use (once per process; a no-op when already present), so the app needs
/// no separate setup step.
/// </summary>
internal sealed class PlaywrightBrowserService : IBrowserService, IDisposable
{
    private static readonly string[] _installChromiumArguments = ["install", "chromium"];

    private readonly BrowserOptions _options;
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private bool _chromiumInstalled;

    public PlaywrightBrowserService(BrowserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task<IBrowserSession> OpenSessionAsync(CancellationToken ct = default)
    {
        await EnsureChromiumInstalledAsync(ct).ConfigureAwait(false);

        var playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        IBrowser? browser = null;
        try
        {
            browser = await playwright.Chromium
                .LaunchAsync(new BrowserTypeLaunchOptions { Headless = _options.Headless })
                .WaitAsync(ct)
                .ConfigureAwait(false);

            // A fresh context per session: no cookies or storage leak between automations.
            var context = await browser
                .NewContextAsync(new BrowserNewContextOptions { AcceptDownloads = true })
                .WaitAsync(ct)
                .ConfigureAwait(false);
            var page = await context.NewPageAsync().WaitAsync(ct).ConfigureAwait(false);
            page.SetDefaultTimeout((float)_options.ActionTimeout.TotalMilliseconds);

            var ownedBrowser = browser;
            return new PlaywrightBrowserSession(page, async () =>
            {
                await ownedBrowser.CloseAsync().ConfigureAwait(false);
                playwright.Dispose();
            });
        }
        catch
        {
            if (browser is not null)
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }

            playwright.Dispose();
            throw;
        }
    }

    public void Dispose() => _installLock.Dispose();

    private async Task EnsureChromiumInstalledAsync(CancellationToken ct)
    {
        await _installLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_chromiumInstalled)
            {
                return;
            }

            // Synchronous CLI entry point (downloads the browser when missing): keep it off the caller's thread.
            var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(_installChromiumArguments), ct)
                .ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Could not install the Chromium browser used by the automations (Playwright exit code {exitCode}).");
            }

            _chromiumInstalled = true;
        }
        finally
        {
            _installLock.Release();
        }
    }
}
