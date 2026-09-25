using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Infrastructure.Features.AzureDevOps;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Features.AzureDevOps;

/// <summary>
/// A fake Azure DevOps organization behind a stub HTTP handler: <c>connectionData</c> always answers as
/// <see cref="ME_NAME"/>; every other request goes to the test's router (unrouted → 404). The real
/// <see cref="AzureDevOpsClient"/>, <see cref="AzureDevOpsService"/> and
/// <see cref="AzureDevOpsConnectionService"/> run on top, so URL building, auth and JSON mapping are
/// exercised for real.
/// </summary>
internal sealed class AzureDevOpsFixture : IDisposable
{
    public const string ORG = "https://dev.azure.com/cegid/";
    public const string PAT = "stored-pat";
    public const string ME_ID = "5d6f1c2a-user";
    public const string ME_NAME = "Jane Doe";

    private const string CONNECTION_DATA_PATH = "/cegid/_apis/connectionData";

    public AzureDevOpsFixture(
        Func<HttpRequestMessage, HttpResponseMessage?>? route = null,
        string[]? projects = null,
        string? organizationUrl = ORG,
        string? storedToken = PAT)
    {
        Handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath == CONNECTION_DATA_PATH
                ? ConnectionDataResponse?.Invoke(request) ?? StubHttpMessageHandler.Json(
                    $$$"""{"authenticatedUser":{"id":"{{{ME_ID}}}","providerDisplayName":"{{{ME_NAME}}}"}}""")
                : route?.Invoke(request) ?? StubHttpMessageHandler.Status(HttpStatusCode.NotFound));

        if (storedToken is not null)
        {
            Credentials.Save(CredentialKeys.AZURE_DEVOPS_PAT, storedToken);
        }

        var options = Live.Of(new AzureDevOpsOptions(organizationUrl is null ? null : new Uri(organizationUrl), projects ?? ["Alpha"]));
        Factory = new StubHttpClientFactory(Handler);
        Client = new AzureDevOpsClient(Factory, options, Credentials);
        Service = new AzureDevOpsService(Client, options, NullLogger<AzureDevOpsService>.Instance);
        Connection = new AzureDevOpsConnectionService(Client, Credentials);
    }

    public StubHttpMessageHandler Handler { get; }

    public StubHttpClientFactory Factory { get; }

    public InMemoryCredentialStore Credentials { get; } = new();

    public AzureDevOpsClient Client { get; }

    public AzureDevOpsService Service { get; }

    public AzureDevOpsConnectionService Connection { get; }

    /// <summary>Overrides the <c>connectionData</c> answer (e.g. to reject a token).</summary>
    public Func<HttpRequestMessage, HttpResponseMessage?>? ConnectionDataResponse { get; set; }

    /// <summary>Requests other than the identity lookup.</summary>
    public IEnumerable<RecordedRequest> DataRequests => Handler.Requests.Where(r => r.Uri.AbsolutePath != CONNECTION_DATA_PATH);

    public void Dispose() => Handler.Dispose();
}
