using System.ComponentModel;
using System.Globalization;
using MyWorkHub.Core.Features.CliAgent;
using MyWorkHub.Infrastructure.Configuration;
using MyWorkHub.Infrastructure.Features.Workspace;
using MyWorkHub.Infrastructure.Processes;

namespace MyWorkHub.Infrastructure.Features.CliAgent;

/// <summary>
/// <see cref="ICliAgentRunner"/> over the Claude Code CLI in headless print mode (<c>claude -p</c>). This is
/// the only code that builds a <c>claude</c> command line, so the security rules are enforced here once:
/// <list type="bullet">
/// <item>the system prompt is always the <c>--system-prompt</c> argument, the user message always stdin;</item>
/// <item><c>--bare</c> is never passed — it disables the OAuth/keychain login the user already has;</item>
/// <item>without a working directory the run happens in a fresh empty temp directory, deleted afterwards.</item>
/// </list>
/// </summary>
internal sealed class ClaudeCliAgentRunner : ICliAgentRunner
{
    private const string TEMP_DIRECTORY_PREFIX = "myworkhub-claude-";
    private const int ERROR_SNIPPET_LENGTH = 600;

    private readonly IProcessRunner _processes;
    private readonly LiveOptions<WorkspaceOptions> _options;

    public ClaudeCliAgentRunner(IProcessRunner processes, LiveOptions<WorkspaceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(options);
        _processes = processes;
        _options = options;
    }

    public async Task<string> RunAsync(CliAgentRequest request, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserMessage);

        // Isolation: nothing but the prompt may reach the model — no CLAUDE.md, no repository files.
        var claudeExecutable = _options.Current.ClaudeExecutablePath;
        var isolatedDirectory = request.WorkingDirectory is null
            ? Directory.CreateTempSubdirectory(TEMP_DIRECTORY_PREFIX)
            : null;
        try
        {
            var processRequest = new ProcessRequest(
                claudeExecutable,
                BuildArguments(request),
                request.WorkingDirectory ?? isolatedDirectory!.FullName,
                StandardInput: request.UserMessage);

            ProcessResult result;
            try
            {
                result = await _processes.RunAsync(processRequest, progress, ct).ConfigureAwait(false);
            }
            catch (Win32Exception ex)
            {
                throw new CliAgentException(
                    $"Could not start the Claude CLI ('{claudeExecutable}'). Install Claude Code and sign in, " +
                    $"or set its path in Settings → Workspace ({WorkspaceOptions.SECTION}:ClaudeExecutablePath).", ex);
            }

            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                throw new CliAgentException(string.Create(CultureInfo.InvariantCulture,
                    $"The Claude CLI exited with code {result.ExitCode}: {Snippet(detail)}"));
            }

            return result.StandardOutput.Trim();
        }
        finally
        {
            if (isolatedDirectory is not null)
            {
                DeleteQuietly(isolatedDirectory);
            }
        }
    }

    /// <summary>
    /// The exact <c>claude</c> argument list for <paramref name="request"/>. The user message is deliberately
    /// absent: it goes over stdin. <c>--bare</c> is deliberately never emitted.
    /// </summary>
    internal static IReadOnlyList<string> BuildArguments(CliAgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> arguments = ["-p", "--output-format", "text", "--system-prompt", request.SystemPrompt];

        if (!string.IsNullOrWhiteSpace(request.ModelId))
        {
            arguments.AddRange(["--model", request.ModelId]);
        }

        if (request.DisallowedTools is { Count: > 0 } disallowed)
        {
            arguments.AddRange(["--disallowedTools", string.Join(',', disallowed)]);
        }

        if (request.AllowedTools is { Count: > 0 } allowed)
        {
            // One argv entry per rule: rules such as "Bash(git diff:*)" contain spaces and commas are ambiguous.
            arguments.Add("--allowedTools");
            arguments.AddRange(allowed);
        }

        if (request.StrictMcpConfig)
        {
            arguments.Add("--strict-mcp-config");
        }

        return arguments;
    }

    private static string Snippet(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= ERROR_SNIPPET_LENGTH ? trimmed : trimmed[..ERROR_SNIPPET_LENGTH] + "…";
    }

    private static void DeleteQuietly(DirectoryInfo directory)
    {
        try
        {
            directory.Delete(recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file still locked by a lingering child process; the OS temp cleanup will get it.
        }
    }
}
