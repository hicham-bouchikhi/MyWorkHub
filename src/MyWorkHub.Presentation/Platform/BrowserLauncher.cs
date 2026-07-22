using System.Diagnostics;
using MyWorkHub.Core.Abstractions;

namespace MyWorkHub.UI.Platform;

/// <summary>
/// <see cref="IBrowserLauncher"/> backed by the OS shell. <c>UseShellExecute = true</c>
/// hands the URL to the default browser. Best-effort: launch failures are swallowed so a
/// bad/blocked link never crashes a command.
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Opening a browser is best-effort; nothing actionable if the shell refuses.
        }
    }
}
