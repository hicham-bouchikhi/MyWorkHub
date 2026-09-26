using MyWorkHub.Infrastructure.Processes;

namespace MyWorkHub.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Records every <see cref="ProcessRequest"/> instead of spawning anything. Each call is answered by
/// <see cref="Respond"/> (default: exit 0, no output); the answer's stdout is also streamed line by line.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessRequest> Requests { get; } = [];

    /// <summary>Per request: whether its working directory existed and was empty while "running".</summary>
    public List<(bool Existed, bool WasEmpty)> WorkingDirectoryStates { get; } = [];

    public Func<ProcessRequest, ProcessResult> Respond { get; set; } = _ => new ProcessResult(0, "", "");

    public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Requests.Add(request);

        var existed = Directory.Exists(request.WorkingDirectory);
        WorkingDirectoryStates.Add((existed, existed && !Directory.EnumerateFileSystemEntries(request.WorkingDirectory).Any()));

        var result = Respond(request);
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            output?.Report(line);
        }

        return Task.FromResult(result);
    }
}

/// <summary>An <see cref="IProgress{T}"/> that records synchronously (unlike <see cref="Progress{T}"/>).</summary>
internal sealed class RecordingProgress : IProgress<string>
{
    public List<string> Messages { get; } = [];

    public void Report(string value) => Messages.Add(value);
}
