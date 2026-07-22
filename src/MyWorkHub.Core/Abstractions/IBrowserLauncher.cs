namespace MyWorkHub.Core.Abstractions;

/// <summary>
/// Opens external links (PRs, work items, Outlook/Teams deep links) in the user's
/// default browser. Implemented in the UI layer; injected into view-models so they
/// never touch the OS or any UI toolkit type directly.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>Opens the given absolute URL. Best-effort: a launch failure must not throw.</summary>
    void Open(string url);
}
