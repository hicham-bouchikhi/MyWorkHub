namespace MyWorkHub.Core.Features.GraphAuth;

/// <summary>Outcome of an interactive Microsoft 365 sign-in.</summary>
/// <param name="Succeeded">Whether a token was acquired.</param>
/// <param name="AccountName">The signed-in user name on success.</param>
/// <param name="ErrorMessage">Why sign-in failed, suitable for display, on failure.</param>
public sealed record GraphSignInResult(bool Succeeded, string? AccountName, string? ErrorMessage)
{
    public static GraphSignInResult Success(string accountName) => new(true, accountName, null);

    public static GraphSignInResult Failure(string errorMessage) => new(false, null, errorMessage);
}
