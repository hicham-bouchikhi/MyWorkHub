using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.Ai;

/// <summary>
/// <see cref="IEmailSummaryService"/> backed by the Claude CLI — the same <c>claude</c> binary
/// the code-review feature uses. It runs <c>claude -p</c> in a throwaway empty temp folder (so
/// no project files leak into the prompt), feeding the prompt on stdin, and reuses the CLI's own
/// logged-in session for auth. No <c>ANTHROPIC_API_KEY</c> is required; the claude executable
/// path is shared with the review workspace settings.
/// </summary>
public sealed partial class ClaudeCliEmailSummaryService : IEmailSummaryService
{
    private readonly WorkspaceOptions _options;
    private readonly ILogger<ClaudeCliEmailSummaryService> _logger;

    public ClaudeCliEmailSummaryService(
        IOptions<WorkspaceOptions> options,
        ILogger<ClaudeCliEmailSummaryService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _logger = logger;
    }

    private string ClaudeExe =>
        string.IsNullOrWhiteSpace(_options.ClaudeExecutablePath) ? "claude" : _options.ClaudeExecutablePath;

    [LoggerMessage(Level = LogLevel.Warning, Message = "claude stderr: {StdErr}")]
    private partial void LogClaudeStdErr(string stdErr);

    public async Task<string> SummarizeAsync(IReadOnlyList<EmailItem> emails, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        if (emails.Count == 0)
        {
            return "No emails to summarise.";
        }

        // The trusted instructions go to the CLI as a system-prompt argument; only the untrusted
        // email content is piped over stdin, quarantined inside an unguessable nonce boundary so
        // it cannot forge the block markers to break out and pose as instructions.
        var boundary = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var userMessage = EmailSummaryPrompt.BuildUserMessage(emails, boundary);
        var arguments = ClaudeCliArguments.ForSummary(EmailSummaryPrompt.SYSTEM);

        // Empty temp folder so the CLI has no project context (CLAUDE.md, files) to read.
        var workDir = Directory.CreateTempSubdirectory("cwh-summary-");
        try
        {
            var result = await RunClaudeAsync(arguments, userMessage, workDir.FullName, ct).ConfigureAwait(false);
            var text = result.Trim();
            return string.IsNullOrWhiteSpace(text) ? "The AI returned no summary." : text;
        }
        catch (Win32Exception ex)
        {
            // Executable not found on PATH / at the configured location.
            throw new InvalidOperationException(
                "The Claude CLI was not found. Install it, or set its path in Settings → Preferences → Workspace.",
                ex);
        }
        finally
        {
            TryDeleteDirectory(workDir.FullName);
        }
    }

    private async Task<string> RunClaudeAsync(
        IReadOnlyList<string> arguments, string stdin, string workingDirectory, CancellationToken ct)
    {
        var info = new ProcessStartInfo(ClaudeExe)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = info };
        process.Start();

        // Terminate the child tree on cancellation so a cancelled summary leaves nothing running.
        using var registration = ct.Register(static state => TryKill((Process)state!), process);

        // Start reading before writing stdin so a large prompt can't deadlock on a full pipe.
        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        await process.StandardInput.WriteAsync(stdin.AsMemory(), ct).ConfigureAwait(false);
        process.StandardInput.Close();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            LogClaudeStdErr(stdErr);
        }

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
            throw new InvalidOperationException(
                $"claude exited with code {process.ExitCode}. {detail}".Trim());
        }

        return stdOut;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited between the check and Kill.
        }
        catch (Win32Exception)
        {
            // The OS refused to terminate it; nothing more we can do.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp folder.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temp folder.
        }
    }
}
