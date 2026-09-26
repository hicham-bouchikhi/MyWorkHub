using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Playwright;
using MyWorkHub.Infrastructure.Features.Automations.Browser;

namespace MyWorkHub.Infrastructure.Tests.Features.Automations;

/// <summary>
/// The wrapper's contract with Playwright, checked against a scripted <see cref="IPage"/> (no real browser:
/// launching Chromium would need a browser download and adds nothing these tests do not already prove).
/// </summary>
public sealed class PlaywrightBrowserSessionTests : IDisposable
{
    private readonly DirectoryInfo _downloads = Directory.CreateTempSubdirectory("mwh-downloads-");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _downloads.Delete(recursive: true);

    [Fact]
    public async Task Should_register_the_download_waiter_before_clicking_so_the_download_event_is_not_missed()
    {
        var page = ScriptedPage.Create("attestation.pdf");
        await using var session = new PlaywrightBrowserSession(page.Page, () => ValueTask.CompletedTask);

        // The scripted page fires the download event during the click and drops it when no waiter is
        // registered yet — exactly the race a click-then-wait implementation loses.
        var path = await session.DownloadFileAsync("#download", _downloads.FullName, Ct);

        Assert.Equal(["WaitForDownloadAsync", "ClickAsync:#download", $"SaveAsAsync:{path}"], page.Calls);
    }

    [Fact]
    public async Task Should_save_the_download_to_disk_under_the_suggested_name_and_return_its_path()
    {
        var page = ScriptedPage.Create("attestation.pdf");
        await using var session = new PlaywrightBrowserSession(page.Page, () => ValueTask.CompletedTask);

        var path = await session.DownloadFileAsync("#download", _downloads.FullName, Ct);

        Assert.Equal(Path.Combine(_downloads.FullName, "attestation.pdf"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Should_not_overwrite_an_earlier_download_with_the_same_name()
    {
        await File.WriteAllTextAsync(Path.Combine(_downloads.FullName, "attestation.pdf"), "earlier", Ct);
        var page = ScriptedPage.Create("attestation.pdf");
        await using var session = new PlaywrightBrowserSession(page.Page, () => ValueTask.CompletedTask);

        var path = await session.DownloadFileAsync("#download", _downloads.FullName, Ct);

        Assert.Equal(Path.Combine(_downloads.FullName, "attestation (2).pdf"), path);
    }

    [Theory]
    [InlineData("../../evil.sh", "evil.sh")]
    [InlineData("..\\..\\evil.bat", "evil.bat")]
    [InlineData("", "download")]
    [InlineData("..", "download")]
    public void Should_keep_a_hostile_suggested_name_inside_the_download_folder(string suggested, string expected)
    {
        Assert.Equal(expected, PlaywrightBrowserSession.SafeFileName(suggested));
    }

    [Fact]
    public async Task Should_close_the_browser_once_when_disposed()
    {
        var closed = 0;
        var session = new PlaywrightBrowserSession(ScriptedPage.Create("x").Page, () =>
        {
            closed++;
            return ValueTask.CompletedTask;
        });

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task Should_not_touch_the_page_when_already_cancelled()
    {
        var page = ScriptedPage.Create("x");
        await using var session = new PlaywrightBrowserSession(page.Page, () => ValueTask.CompletedTask);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ClickAsync("#go", cts.Token));

        Assert.Empty(page.Calls);
    }

    [Fact]
    public void Should_read_the_browser_options_with_safe_defaults()
    {
        var defaults = BrowserOptions.FromConfiguration(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        Assert.True(defaults.Headless);
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.ActionTimeout);
    }

    /// <summary>
    /// A <see cref="DispatchProxy"/>-based <see cref="IPage"/> implementing just the members the session
    /// uses. A click fires the download event; the event reaches only a waiter registered before it.
    /// </summary>
    [SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy derives a runtime type from it, so it cannot be sealed.")]
    internal class ScriptedPage : DispatchProxy
    {
        private TaskCompletionSource<IDownload>? _waiter;
        private bool _eventMissed;
        private string _suggestedName = "";

        public List<string> Calls { get; } = [];

        public IPage Page { get; private set; } = null!;

        public static ScriptedPage Create(string suggestedName)
        {
            var page = Create<IPage, ScriptedPage>();
            var script = (ScriptedPage)(object)page;
            script._suggestedName = suggestedName;
            script.Page = page;
            return script;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case nameof(IPage.WaitForDownloadAsync):
                    Calls.Add(targetMethod.Name);
                    _waiter = new TaskCompletionSource<IDownload>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (_eventMissed)
                    {
                        _waiter.SetException(new TimeoutException("Timed out waiting for the download: the event already fired."));
                    }

                    return _waiter.Task;

                case nameof(IPage.ClickAsync):
                    Calls.Add($"{targetMethod.Name}:{args![0]}");
                    if (_waiter is null)
                    {
                        _eventMissed = true;
                    }
                    else
                    {
                        _waiter.SetResult(ScriptedDownload.Create(_suggestedName, Calls));
                    }

                    return Task.CompletedTask;

                default:
                    Calls.Add(targetMethod.Name);
                    return targetMethod.ReturnType == typeof(Task) ? Task.CompletedTask : null;
            }
        }
    }

    /// <summary>A download whose <c>SaveAsAsync</c> writes a placeholder file where it is told to.</summary>
    [SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy derives a runtime type from it, so it cannot be sealed.")]
    internal class ScriptedDownload : DispatchProxy
    {
        private string _suggestedName = "";
        private List<string> _calls = [];

        public static IDownload Create(string suggestedName, List<string> calls)
        {
            var download = Create<IDownload, ScriptedDownload>();
            var script = (ScriptedDownload)(object)download;
            script._suggestedName = suggestedName;
            script._calls = calls;
            return download;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            switch (targetMethod.Name)
            {
                case "get_" + nameof(IDownload.SuggestedFilename):
                    return _suggestedName;

                case nameof(IDownload.SaveAsAsync):
                    var path = (string)args![0]!;
                    _calls.Add($"{targetMethod.Name}:{path}");
                    File.WriteAllText(path, "%PDF");
                    return Task.CompletedTask;

                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}
