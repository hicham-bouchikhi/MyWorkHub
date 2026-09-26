using System.Globalization;
using MyWorkHub.Core.Features.Automations;
using MyWorkHub.Infrastructure.Features.Automations.Browser;

namespace MyWorkHub.Infrastructure.Features.Automations.Sites;

/// <summary>Where the automations write files: downloads, and screenshots of failed site runs.</summary>
/// <param name="DownloadsDirectory">Folder downloaded files (attestations) are saved into.</param>
/// <param name="ScreenshotsDirectory">Folder a screenshot of the page is saved into when a site script fails.</param>
internal sealed record AutomationPaths(string DownloadsDirectory, string ScreenshotsDirectory);

/// <summary>
/// Runs one scripted visit of an external site: checks the stored login, opens a fresh browser, opens the
/// site and signs in with the configured login-form selectors, then runs the site-specific part. When the
/// browser part fails, a screenshot of the page is saved and its path added to the error, so a failed run
/// in the history can be diagnosed (e.g. a site whose layout changed).
/// </summary>
internal sealed class SiteScriptRunner
{
    private const string SCREENSHOT_TIMESTAMP_FORMAT = "yyyyMMdd-HHmmss";

    private readonly IBrowserService _browser;
    private readonly IAutomationCredentialService _credentials;
    private readonly AutomationPaths _paths;
    private readonly TimeProvider _timeProvider;

    public SiteScriptRunner(
        IBrowserService browser,
        IAutomationCredentialService credentials,
        AutomationPaths paths,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _browser = browser;
        _credentials = credentials;
        _paths = paths;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Signs in to <paramref name="site"/> and runs <paramref name="script"/> on the signed-in page.
    /// Everything that can be checked without a browser (login saved, base URL and login selectors
    /// configured) is checked first, so a setup gap fails fast without launching Chromium.
    /// </summary>
    public async Task<T> RunAsync<T>(SiteOptions site, Func<IBrowserSession, Task<T>> script, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(script);

        var login = _credentials.Get(site.Site)
                    ?? throw new AutomationException(
                        $"{site.DisplayName}: no login saved. Add it under Site logins on the Automations page.");
        var url = site.RequireBaseUrl();
        var userNameField = site.RequireSelector(SiteSelectorKeys.USER_NAME_FIELD);
        var passwordField = site.RequireSelector(SiteSelectorKeys.PASSWORD_FIELD);
        var loginButton = site.RequireSelector(SiteSelectorKeys.LOGIN_BUTTON);

        var session = await _browser.OpenSessionAsync(ct).ConfigureAwait(false);
        await using (session.ConfigureAwait(false))
        {
            try
            {
                await session.GoToAsync(url, ct).ConfigureAwait(false);
                await session.FillAsync(userNameField, login.UserName, ct).ConfigureAwait(false);
                await session.FillAsync(passwordField, login.Password, ct).ConfigureAwait(false);
                await session.ClickAsync(loginButton, ct).ConfigureAwait(false);

                return await script(session).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not AutomationException)
            {
                var screenshot = await TryCaptureScreenshotAsync(session, site).ConfigureAwait(false);
                var hint = screenshot is null ? "" : $" A screenshot of the page was saved to {screenshot}.";
                throw new AutomationException($"{site.DisplayName}: {ex.Message}{hint}", ex);
            }
        }
    }

    // Best effort: a screenshot failure (browser already gone) must not hide the original error.
    private async Task<string?> TryCaptureScreenshotAsync(IBrowserSession session, SiteOptions site)
    {
        var stamp = _timeProvider.GetLocalNow().ToString(SCREENSHOT_TIMESTAMP_FORMAT, CultureInfo.InvariantCulture);
        var path = Path.Combine(_paths.ScreenshotsDirectory, $"{site.Site}-{stamp}.png");
        try
        {
            await session.CaptureScreenshotAsync(path, CancellationToken.None).ConfigureAwait(false);
            return path;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
