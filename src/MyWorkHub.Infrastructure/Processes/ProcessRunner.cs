using System.Diagnostics;
using System.Text;

namespace MyWorkHub.Infrastructure.Processes;

/// <summary><see cref="IProcessRunner"/> over <see cref="Process"/> (no shell, redirected UTF-8 streams).</summary>
internal sealed class ProcessRunner : IProcessRunner
{
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new Process { StartInfo = CreateStartInfo(request) };
        process.Start(); // Win32Exception when the program cannot be found or started.

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        try
        {
            // Both pipes are drained concurrently from the start so a chatty stderr can never block the child.
            var stdoutPump = PumpAsync(process.StandardOutput, stdout, output, ct);
            var stderrPump = PumpAsync(process.StandardError, stderr, null, ct);

            await WriteInputAsync(process, request.StandardInput, ct).ConfigureAwait(false);
            await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = _utf8,
            StandardOutputEncoding = _utf8,
            StandardErrorEncoding = _utf8,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in request.Environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private static async Task WriteInputAsync(Process process, string? input, CancellationToken ct)
    {
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), ct).ConfigureAwait(false);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The child exited (or closed stdin) before reading everything; its exit code tells the story.
        }
    }

    private static async Task PumpAsync(StreamReader reader, StringBuilder sink, IProgress<string>? lines, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            sink.AppendLine(line);
            lines?.Report(line);
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
