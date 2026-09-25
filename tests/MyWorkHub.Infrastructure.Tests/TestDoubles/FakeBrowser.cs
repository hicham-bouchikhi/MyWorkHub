using MyWorkHub.Infrastructure.Features.Automations.Browser;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>An <see cref="IBrowserService"/> handing out <see cref="FakeBrowserSession"/>s that record every action.</summary>
internal sealed class FakeBrowserService : IBrowserService
{
    public List<FakeBrowserSession> Sessions { get; } = [];

    /// <summary>When set, the action whose description starts with this text throws.</summary>
    public string? FailOn { get; set; }

    public Task<IBrowserSession> OpenSessionAsync(CancellationToken ct = default)
    {
        var session = new FakeBrowserSession(FailOn);
        Sessions.Add(session);
        return Task.FromResult<IBrowserSession>(session);
    }
}

internal sealed class FakeBrowserSession(string? failOn) : IBrowserSession
{
    public const string DOWNLOADED_FILE_NAME = "attestation.pdf";

    public List<string> Actions { get; } = [];

    public bool Disposed { get; private set; }

    public Task GoToAsync(Uri url, CancellationToken ct = default) => Record($"goto {url.AbsoluteUri}");

    public Task FillAsync(string selector, string value, CancellationToken ct = default) => Record($"fill {selector}={value}");

    public Task ClickAsync(string selector, CancellationToken ct = default) => Record($"click {selector}");

    public async Task<string> DownloadFileAsync(string triggerSelector, string destinationDirectory, CancellationToken ct = default)
    {
        await Record($"download {triggerSelector} -> {destinationDirectory}");
        return Path.Combine(destinationDirectory, DOWNLOADED_FILE_NAME);
    }

    public Task CaptureScreenshotAsync(string destinationPath, CancellationToken ct = default)
    {
        Actions.Add($"screenshot {destinationPath}");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private Task Record(string action)
    {
        Actions.Add(action);
        return failOn is not null && action.StartsWith(failOn, StringComparison.Ordinal)
            ? Task.FromException(new TimeoutException($"Timeout 30000ms exceeded waiting for {action}."))
            : Task.CompletedTask;
    }
}
