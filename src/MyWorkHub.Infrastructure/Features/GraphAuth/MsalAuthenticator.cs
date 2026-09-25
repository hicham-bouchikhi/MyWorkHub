using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using MyWorkHub.Core;

namespace MyWorkHub.Infrastructure.Features.GraphAuth;

/// <summary>
/// <see cref="IMsalAuthenticator"/> over a real MSAL public-client application. Interactive sign-in always
/// uses the system browser (<c>WithUseEmbeddedWebView(false)</c>), which Cegid SSO requires. The token
/// cache is persisted to <see cref="AppPaths.MsalTokenCachePath"/>, encrypted per OS by
/// <see cref="MsalCacheHelper"/> (DPAPI / macOS Keychain / Linux libsecret).
/// <para>
/// The MSAL application is built lazily on first use, not in the constructor: resolving the Graph
/// services (e.g. during startup composition validation) must stay free of I/O and must not fail when
/// the Azure AD registration is not configured yet — that is reported when the user actually signs in.
/// </para>
/// </summary>
internal sealed partial class MsalAuthenticator : IMsalAuthenticator, IDisposable
{
    // Loopback redirect for the system browser: MSAL listens on a free localhost port.
    private const string REDIRECT_URI = "http://localhost";

    private const string KEYRING_SCHEMA = "com.myworkhub.app";
    private const string KEYRING_LABEL = "MyWorkHub Microsoft 365 token cache";
    private const string KEYCHAIN_SERVICE = "MyWorkHub";
    private const string KEYCHAIN_ACCOUNT = "MsalTokenCache";

    private readonly AzureAdOptions _options;
    private readonly ILogger<MsalAuthenticator> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private IPublicClientApplication? _app;

    public MsalAuthenticator(AzureAdOptions options, ILogger<MsalAuthenticator> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    public async Task<string?> GetAccountNameAsync(CancellationToken ct = default)
    {
        var account = await GetCachedAccountAsync(ct).ConfigureAwait(false);
        return account?.Username;
    }

    public async Task<string?> AcquireTokenSilentAsync(CancellationToken ct = default)
    {
        var app = await GetAppAsync(ct).ConfigureAwait(false);
        var account = await GetCachedAccountAsync(ct).ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        try
        {
            var result = await app.AcquireTokenSilent(GraphScopes.Delegated, account)
                .ExecuteAsync(ct)
                .ConfigureAwait(false);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            // Refresh token expired/revoked or new consent needed: only an explicit sign-in can fix it.
            return null;
        }
    }

    public async Task<string> AcquireTokenInteractiveAsync(CancellationToken ct = default)
    {
        var app = await GetAppAsync(ct).ConfigureAwait(false);
        var result = await app.AcquireTokenInteractive(GraphScopes.Delegated)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync(ct)
            .ConfigureAwait(false);
        return result.Account?.Username ?? "";
    }

    public async Task SignOutAsync(CancellationToken ct = default)
    {
        var app = await GetAppAsync(ct).ConfigureAwait(false);
        foreach (var account in await app.GetAccountsAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            await app.RemoveAsync(account).ConfigureAwait(false);
        }
    }

    public void Dispose() => _initGate.Dispose();

    private async Task<IAccount?> GetCachedAccountAsync(CancellationToken ct)
    {
        var app = await GetAppAsync(ct).ConfigureAwait(false);
        var accounts = await app.GetAccountsAsync().ConfigureAwait(false);
        return accounts.FirstOrDefault();
    }

    private async Task<IPublicClientApplication> GetAppAsync(CancellationToken ct)
    {
        await _initGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_app is null)
            {
                var app = BuildApp();
                await RegisterPersistentCacheAsync(app).ConfigureAwait(false);
                _app = app;
            }

            return _app;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private IPublicClientApplication BuildApp()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException(
                $"Microsoft 365 sign-in is not configured: set {AzureAdOptions.SECTION}:{nameof(AzureAdOptions.ClientId)} " +
                $"(and {nameof(AzureAdOptions.TenantId)}) in {AppPaths.UserAppSettingsPath}.");
        }

        return PublicClientApplicationBuilder
            .Create(_options.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, _options.TenantId)
            .WithRedirectUri(REDIRECT_URI)
            .Build();
    }

    private async Task RegisterPersistentCacheAsync(IPublicClientApplication app)
    {
        // Linux needs explicit libsecret metadata (the defaults are null and fail); macOS gets explicit
        // keychain names. Each setting only applies on its own OS.
        var storage = new StorageCreationPropertiesBuilder(
                Path.GetFileName(AppPaths.MsalTokenCachePath),
                Path.GetDirectoryName(AppPaths.MsalTokenCachePath))
            .WithLinuxKeyring(
                KEYRING_SCHEMA,
                MsalCacheHelper.LinuxKeyRingDefaultCollection,
                KEYRING_LABEL,
                new KeyValuePair<string, string>("product", "MyWorkHub"),
                new KeyValuePair<string, string>("version", "1"))
            .WithMacKeyChain(KEYCHAIN_SERVICE, KEYCHAIN_ACCOUNT)
            .Build();

        try
        {
            var helper = await MsalCacheHelper.CreateAsync(storage).ConfigureAwait(false);
            helper.VerifyPersistence();
            helper.RegisterCache(app.UserTokenCache);
        }
        catch (MsalCachePersistenceException ex)
        {
            // Typically Linux without a running secret service. Never fall back to a plaintext file:
            // keep the cache in memory, so sign-in works but lasts only for this session.
            LogCacheNotPersisted(ex);
        }
    }

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning,
        Message = "The Microsoft 365 token cache cannot be stored securely on this machine; sign-in will last for this session only.")]
    private partial void LogCacheNotPersisted(Exception exception);
}
