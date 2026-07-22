using System.Net;
using System.Text;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>Routes HTTP calls to a caller-supplied responder and records every request.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Request bodies captured at send time (the service disposes its requests afterwards), aligned by index with <see cref="Requests"/>.</summary>
    public List<string?> RequestBodies { get; } = [];

    public static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    public string? BodyFor(HttpRequestMessage request) => RequestBodies[Requests.IndexOf(request)];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return _responder(request);
    }
}
