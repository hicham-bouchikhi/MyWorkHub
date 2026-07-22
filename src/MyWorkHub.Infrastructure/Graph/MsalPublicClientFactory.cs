using MyWorkHub.Core;
using MyWorkHub.Core.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace MyWorkHub.Infrastructure.Graph;

/// <summary>
/// Builds the MSAL public-client application used for interactive sign-in, and wires
/// the DPAPI-backed token cache on disk (ARCHITECTURE.md §8, T011).
/// </summary>
public static class MsalPublicClientFactory
{
    private const string CACHE_FILE_NAME = "msal_token_cache.bin";

    /// <summary>System-browser loopback redirect — MSAL listens on a free localhost port.</summary>
    private const string REDIRECT_URI = "http://localhost";

    public static IPublicClientApplication Create(AzureAdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new InvalidOperationException(
                "AzureAD:ClientId is not configured. Register the Azure AD app (TASKS.md T010) " +
                "and set AzureAD:ClientId in appsettings.json before signing in.");
        }

        return PublicClientApplicationBuilder
            .Create(options.ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, options.TenantId)
            .WithRedirectUri(REDIRECT_URI)
            .Build();
    }

    /// <summary>
    /// Persists the user token cache to <c>~/.MyWorkHub/msal_token_cache.bin</c>,
    /// encrypted with Windows DPAPI (the cache helper's default on Windows).
    /// </summary>
    public static async Task RegisterTokenCacheAsync(IPublicClientApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var storage = new StorageCreationPropertiesBuilder(CACHE_FILE_NAME, AppPaths.RootDir).Build();
        var helper = await MsalCacheHelper.CreateAsync(storage).ConfigureAwait(false);
        helper.RegisterCache(app.UserTokenCache);
    }
}
