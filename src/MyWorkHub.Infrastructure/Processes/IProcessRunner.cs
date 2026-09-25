namespace MyWorkHub.Infrastructure.Processes;

/// <summary>One external program invocation. Never goes through a shell: arguments are passed verbatim.</summary>
/// <param name="FileName">Executable name (resolved on <c>PATH</c>) or full path.</param>
/// <param name="Arguments">Arguments, one element per argv entry (no quoting needed or applied).</param>
/// <param name="WorkingDirectory">Directory the program runs in.</param>
/// <param name="StandardInput">Text written to the program's standard input, which is then closed; <c>null</c>
/// closes it immediately so the program never waits on it.</param>
/// <param name="Environment">Extra environment variables for the child process only (e.g. secrets that must
/// stay out of the command line).</param>
internal sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string? StandardInput = null,
    IReadOnlyDictionary<string, string>? Environment = null);

/// <summary>Outcome of a finished <see cref="ProcessRequest"/>.</summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Seam over <see cref="System.Diagnostics.Process"/> so the code that decides <em>what</em> to run (CLI
/// arguments, git steps) is unit-testable without spawning real programs.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>Runs <paramref name="request"/> to completion.</summary>
    /// <param name="request">What to run.</param>
    /// <param name="output">Receives each standard-output line as soon as it is written.</param>
    /// <param name="ct">Cancels the run and kills the whole process tree.</param>
    /// <exception cref="System.ComponentModel.Win32Exception">The program could not be started (typically: not
    /// installed / not on <c>PATH</c>).</exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default);
}
