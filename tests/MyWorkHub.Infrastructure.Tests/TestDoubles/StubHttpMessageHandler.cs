using System.Net;
using System.Text;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>
/// In-process <see cref="HttpMessageHandler"/>: no network. Every request is recorded — method, URI,
/// Authorization header and body are captured at send time, since the caller disposes its request — and
/// answered by the responder, which may also throw to simulate a transport failure.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Status(HttpStatusCode status, string body = "") => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/plain"),
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
        return _respond(request);
    }
}

/// <summary>What <see cref="StubHttpMessageHandler"/> saw of one request.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? Body)
{
    /// <summary>The unescaped query string, e.g. <c>?ids=1,2&amp;api-version=7.1</c>.</summary>
    public string DecodedQuery => Uri.UnescapeDataString(Uri.Query);
}

/// <summary><see cref="IHttpClientFactory"/> whose every client sends through one stub handler.</summary>
internal sealed class StubHttpClientFactory(StubHttpMessageHandler handler) : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    // disposeHandler: false — callers dispose each client, the shared stub must survive.
    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false);
    }
}
