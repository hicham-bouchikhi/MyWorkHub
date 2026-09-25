using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Features.AzureDevOps;

namespace MyWorkHub.Infrastructure.Features.AzureDevOps;

/// <summary>The authenticated Azure DevOps user.</summary>
/// <param name="Id">Identity id (what reviewer / creator filters take).</param>
/// <param name="DisplayName">Display name (what comment @mentions show).</param>
internal sealed record AzureDevOpsUser(string Id, string DisplayName);

/// <summary>
/// Thin raw-REST transport shared by the Azure DevOps services: resolves organization-relative URLs,
/// adds PAT Basic authentication, and turns an authentication failure into
/// <see cref="AzureDevOpsNotConnectedException"/>. Deliberately NOT the Azure DevOps .NET client SDK,
/// which drags in a vulnerable SqlClient dependency. Each call takes a pooled <see cref="HttpClient"/>
/// from <see cref="IHttpClientFactory"/>, so this class is safe to hold in singletons.
/// </summary>
internal sealed class AzureDevOpsClient
{
    /// <summary>Name of the <see cref="IHttpClientFactory"/> client configured by the module.</summary>
    public const string HTTP_CLIENT_NAME = "AzureDevOps";

    /// <summary>
    /// Credential-store key of the personal access token. Same value as the pre-rewrite app used, so a
    /// token saved by it keeps working.
    /// </summary>
    public const string PAT_CREDENTIAL_KEY = "AZDO_PAT";

    // connectionData only exists as a preview resource: the GA "7.1" is rejected with a 400.
    private const string CONNECTION_DATA_URL = "_apis/connectionData?api-version=7.1-preview.1";

    private const int ERROR_SNIPPET_LENGTH = 400;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureDevOpsOptions _options;
    private readonly ICredentialStore _credentials;

    public AzureDevOpsClient(IHttpClientFactory httpClientFactory, AzureDevOpsOptions options, ICredentialStore credentials)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentials);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _credentials = credentials;
    }

    /// <summary>The configured organization root (ends in <c>/</c>).</summary>
    /// <exception cref="AzureDevOpsNotConnectedException">No valid organization URL is configured.</exception>
    public Uri OrganizationUrl => _options.OrganizationUrl
        ?? throw new AzureDevOpsNotConnectedException(
            $"The Azure DevOps organization URL is not configured. Set {AzureDevOpsOptions.SECTION}:OrganizationUrl " +
            "in ~/.MyWorkHub/appsettings.json (e.g. https://dev.azure.com/your-org/).");

    /// <summary>The user the stored token authenticates as.</summary>
    public Task<AzureDevOpsUser> GetCurrentUserAsync(CancellationToken ct = default)
        => GetCurrentUserAsync(StoredTokenOrThrow(), ct);

    /// <summary>The user <paramref name="personalAccessToken"/> authenticates as (used to verify a token before storing it).</summary>
    public async Task<AzureDevOpsUser> GetCurrentUserAsync(string personalAccessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve(CONNECTION_DATA_URL));
        var data = await SendAsync(request, personalAccessToken, AzureDevOpsJson.Default.ConnectionDataDto, ct).ConfigureAwait(false);
        var user = data?.AuthenticatedUser;
        return new AzureDevOpsUser(user?.Id ?? "", user?.ProviderDisplayName ?? "");
    }

    /// <summary>GETs an organization-relative URL (e.g. <c>Project/_apis/git/pullrequests?…</c>) and deserializes the JSON response.</summary>
    public async Task<T?> GetAsync<T>(string relativeUrl, JsonTypeInfo<T> responseType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve(relativeUrl));
        return await SendAsync(request, StoredTokenOrThrow(), responseType, ct).ConfigureAwait(false);
    }

    /// <summary>POSTs <paramref name="body"/> as JSON to an organization-relative URL and deserializes the JSON response.</summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Resolve(relativeUrl))
        {
            Content = JsonContent.Create(body, requestType),
        };
        return await SendAsync(request, StoredTokenOrThrow(), responseType, ct).ConfigureAwait(false);
    }

    private Uri Resolve(string relativeUrl) => new(OrganizationUrl, relativeUrl);

    private string StoredTokenOrThrow()
    {
        // Resolve the organization first so a missing URL is reported before a missing token.
        _ = OrganizationUrl;
        var token = _credentials.Get(PAT_CREDENTIAL_KEY);
        return string.IsNullOrWhiteSpace(token) ? throw new AzureDevOpsNotConnectedException() : token;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, string personalAccessToken, JsonTypeInfo<T> responseType, CancellationToken ct)
    {
        // Azure DevOps PAT auth is HTTP Basic with an empty user name and the token as password.
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + personalAccessToken)));

        using var http = _httpClientFactory.CreateClient(HTTP_CLIENT_NAME);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);

        // A bad token gets a 401 — or, on some endpoints, a 203 carrying the HTML sign-in page.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new AzureDevOpsNotConnectedException(
                "Azure DevOps rejected the personal access token (expired, revoked or for another organization).");
        }

        if (!response.IsSuccessStatusCode)
        {
            // Azure DevOps explains most failures (bad project name, missing scope…) in the body.
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var snippet = body.Length > ERROR_SNIPPET_LENGTH ? body[..ERROR_SNIPPET_LENGTH] : body;
            throw new HttpRequestException(
                $"Azure DevOps {request.Method} {request.RequestUri?.AbsolutePath} failed with {(int)response.StatusCode} {response.StatusCode}. {snippet}",
                inner: null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync(responseType, ct).ConfigureAwait(false);
    }
}
