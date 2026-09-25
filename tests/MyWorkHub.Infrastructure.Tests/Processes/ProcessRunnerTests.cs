using System.ComponentModel;
using MyWorkHub.Infrastructure.Processes;
using MyWorkHub.Infrastructure.Tests.TestDoubles;

namespace MyWorkHub.Infrastructure.Tests.Processes;

/// <summary>Exercises the real process wrapper with the one program guaranteed to exist where tests run: dotnet.</summary>
public sealed class ProcessRunnerTests
{
    private static readonly string _dotnet = Environment.ProcessPath is { } path && Path.GetFileNameWithoutExtension(path) == "dotnet"
        ? path
        : "dotnet";

    [Fact]
    public async Task Should_capture_the_exit_code_and_stream_stdout_lines()
    {
        var progress = new RecordingProgress();

        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(_dotnet, ["--version"], Path.GetTempPath(), StandardInput: "ignored"), progress, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+", result.StandardOutput);
        Assert.Equal([result.StandardOutput.Trim()], progress.Messages);
    }

    [Fact]
    public async Task Should_report_a_non_zero_exit_code_with_stderr()
    {
        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(_dotnet, ["this-command-does-not-exist-" + Guid.NewGuid().ToString("N")], Path.GetTempPath()),
            ct: TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardError + result.StandardOutput));
    }

    [Fact]
    public async Task Should_throw_a_win32_exception_when_the_program_does_not_exist()
    {
        await Assert.ThrowsAsync<Win32Exception>(() => new ProcessRunner().RunAsync(
            new ProcessRequest("no-such-program-" + Guid.NewGuid().ToString("N"), [], Path.GetTempPath()),
            ct: TestContext.Current.CancellationToken));
    }
}
