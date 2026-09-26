namespace MyWorkHub.Core.Features.GraphAuth;

/// <summary>
/// The user's Microsoft 365 (Graph) sign-in, shared by every Graph-backed feature (Email, Calendar,
/// Teams). Interactive sign-in happens ONLY when <see cref="SignInAsync"/> is called explicitly — never
/// implicitly on a data request; a data request made while signed out throws
/// <see cref="GraphNotConnectedException"/> instead.
/// </summary>
public interface IGraphConnectionService
{
    /// <summary>The signed-in account's user name, or <c>null</c> when signed out. Never prompts.</summary>
    Task<string?> GetSignedInAccountAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens the system browser for an interactive sign-in. The token lands in the shared cache, so
    /// every Graph-backed service picks it up silently afterwards. Failures (including the user
    /// closing the browser) are reported in the result, not thrown.
    /// </summary>
    Task<GraphSignInResult> SignInAsync(CancellationToken ct = default);

    /// <summary>Forgets every cached account.</summary>
    Task SignOutAsync(CancellationToken ct = default);
}
