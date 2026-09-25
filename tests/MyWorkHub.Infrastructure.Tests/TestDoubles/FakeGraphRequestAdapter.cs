using Microsoft.Graph;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Abstractions.Store;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>
/// In-process Kiota <see cref="IRequestAdapter"/> for the <c>GraphServiceClient(IRequestAdapter)</c> seam:
/// no network, no token. Every request is recorded (so tests can assert the Graph path, query and
/// headers the service built) and answered with the typed model returned by the responder — or the
/// responder throws to simulate a Graph error.
/// </summary>
internal sealed class FakeGraphRequestAdapter : IRequestAdapter, IDisposable
{
    private readonly Func<RequestInformation, IParsable?> _respond;
    private bool _disposed;

    public FakeGraphRequestAdapter(Func<RequestInformation, IParsable?> respond)
    {
        _respond = respond;
        Client = new GraphServiceClient(this);
    }

    /// <summary>A Graph client whose every call goes through this adapter.</summary>
    public GraphServiceClient Client { get; }

    public List<RequestInformation> Requests { get; } = [];

    public ISerializationWriterFactory SerializationWriterFactory => SerializationWriterFactoryRegistry.DefaultInstance;

    public string? BaseUrl { get; set; }

    public void Dispose()
    {
        // The client disposes its adapter in turn; guard against the round trip.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Client.Dispose();
    }

    public void EnableBackingStore(IBackingStoreFactory backingStoreFactory)
    {
    }

    public Task<ModelType?> SendAsync<ModelType>(
        RequestInformation requestInfo,
        ParsableFactory<ModelType> factory,
        Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
        CancellationToken cancellationToken = default)
        where ModelType : IParsable
    {
        Requests.Add(requestInfo);
        return Task.FromResult((ModelType?)_respond(requestInfo));
    }

    public Task<IEnumerable<ModelType>?> SendCollectionAsync<ModelType>(
        RequestInformation requestInfo,
        ParsableFactory<ModelType> factory,
        Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
        CancellationToken cancellationToken = default)
        where ModelType : IParsable
        => throw new NotSupportedException();

    public Task<ModelType?> SendPrimitiveAsync<ModelType>(
        RequestInformation requestInfo,
        Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IEnumerable<ModelType>?> SendPrimitiveCollectionAsync<ModelType>(
        RequestInformation requestInfo,
        Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task SendNoContentAsync(
        RequestInformation requestInfo,
        Dictionary<string, ParsableFactory<IParsable>>? errorMapping = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<T?> ConvertToNativeRequestAsync<T>(RequestInformation requestInfo, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Helpers for asserting on a recorded Graph request.</summary>
internal static class RecordedRequestExtensions
{
    /// <summary>The request path relative to the Graph v1.0 root, e.g. <c>/me/mailFolders/inbox/messages</c>.</summary>
    public static string GraphPath(this RequestInformation request)
        => request.URI.AbsolutePath.Replace("/v1.0", "", StringComparison.Ordinal);

    /// <summary>The unescaped query string, e.g. <c>?$top=25&amp;$select=id,subject</c>.</summary>
    public static string DecodedQuery(this RequestInformation request)
        => Uri.UnescapeDataString(request.URI.Query);
}
