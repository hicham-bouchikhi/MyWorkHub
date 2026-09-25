namespace MyWorkHub.Core.Features.GraphAuth;

/// <summary>
/// Thrown by a Graph-backed service when no Microsoft 365 token can be obtained without user
/// interaction (never signed in, signed out, or the refresh token expired). Pages catch it to offer
/// a sign-in instead of showing an error; no request is sent to Graph in that case.
/// </summary>
public sealed class GraphNotConnectedException : Exception
{
    private const string DEFAULT_MESSAGE = "Not signed in to Microsoft 365.";

    public GraphNotConnectedException()
        : base(DEFAULT_MESSAGE)
    {
    }

    public GraphNotConnectedException(string message)
        : base(message)
    {
    }

    public GraphNotConnectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
