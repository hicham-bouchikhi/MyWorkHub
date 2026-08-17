using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MyWorkHub.Core;
using MyWorkHub.Core.Abstractions;
using MyWorkHub.Core.Configuration;
using MyWorkHub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyWorkHub.Infrastructure.CodeReview;

/// <summary>
/// <see cref="IPrReviewService"/> backed by the Claude Code CLI. Clones/fetches the PR repo
/// into the configured work folder (authenticating git with the stored AzDO PAT via a
/// per-command <c>http.extraHeader</c> so the token is never persisted in the repo), checks
/// out the source branch, runs <c>claude -p</c> with a scoped read-only + git tool allowlist,
/// and renders the returned Markdown to an HTML report. Auth for <c>claude</c> comes from the
/// user's existing CLI session — no API key is handled here.
/// </summary>
public sealed partial class ClaudeCodePrReviewService : IPrReviewService
{
    // Read-only analysis plus git, so the headless run never stalls on a permission prompt
    // it cannot answer, yet cannot edit files or reach the network.
    private const string ALLOWED_TOOLS = "Read Grep Glob Bash(git:*)";

    private readonly WorkspaceOptions _options;
    private readonly ICredentialStore _credentials;
    private readonly ILogger<ClaudeCodePrReviewService> _logger;

    public ClaudeCodePrReviewService(
        IOptions<WorkspaceOptions> options,
        ICredentialStore credentials,
        ILogger<ClaudeCodePrReviewService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _credentials = credentials;
        _logger = logger;
    }

    private string ClaudeExe =>
        string.IsNullOrWhiteSpace(_options.ClaudeExecutablePath) ? "claude" : _options.ClaudeExecutablePath;

    [LoggerMessage(Level = LogLevel.Warning, Message = "claude stderr: {StdErr}")]
    private partial void LogClaudeStdErr(string stdErr);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not seed review-agent.md into the cloned repo.")]
    private partial void LogSeedFailure(Exception exception);

    public async Task<PrerequisiteCheckResult> CheckPrerequisitesAsync()
    {
        var gitFound = await ToolRespondsAsync("git", "--version").ConfigureAwait(false);
        var claudeFound = await ToolRespondsAsync(ClaudeExe, "--version").ConfigureAwait(false);
        var workFolderIssue = CheckWorkFolder();
        return new PrerequisiteCheckResult(gitFound, claudeFound, workFolderIssue);
    }

    public async Task<string> ReviewAsync(
        PullRequestItem pr,
        IProgress<string>? progress = null,
        string? agentFilePath = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pr);

        var workFolderIssue = CheckWorkFolder();
        if (workFolderIssue is not null)
        {
            throw new InvalidOperationException(workFolderIssue);
        }

        Directory.CreateDirectory(_options.WorkFolderPath);
        var repoDir = Path.Combine(_options.WorkFolderPath, SanitizeSegment(pr.Repository));

        var pat = _credentials.Get(CredentialKeys.AZURE_DEVOPS_PAT);
        if (string.IsNullOrWhiteSpace(pat))
        {
            throw new InvalidOperationException("No Azure DevOps personal access token is configured.");
        }

        progress?.Report($"Preparing workspace at {repoDir}");
        await CloneOrFetchAsync(pr, repoDir, pat, progress, ct).ConfigureAwait(false);
        await CheckoutSourceBranchAsync(pr, repoDir, progress, ct).ConfigureAwait(false);

        var prompt = BuildPrompt(pr, repoDir, agentFilePath, progress);
        var markdown = await RunClaudeAsync(prompt, repoDir, progress, ct).ConfigureAwait(false);

        progress?.Report("Generating HTML report");
        return ReviewHtmlExporter.Export(pr, markdown);
    }

    private static async Task CloneOrFetchAsync(
        PullRequestItem pr, string repoDir, string pat, IProgress<string>? progress, CancellationToken ct)
    {
        var authArgs = BuildGitAuthArgs(pat);

        if (Directory.Exists(Path.Combine(repoDir, ".git")))
        {
            // Repo already present — refresh remote refs (equivalent to a pull once we reset
            // the branch to origin below), no re-clone.
            progress?.Report("Repository already cloned — fetching latest changes (git fetch origin --prune)");
            var fetch = await RunProcessAsync("git", [.. authArgs, "-C", repoDir, "fetch", "origin", "--prune"], null, ct)
                .ConfigureAwait(false);
            ThrowIfFailed("git fetch", fetch);
            return;
        }

        if (string.IsNullOrWhiteSpace(pr.CloneUrl))
        {
            throw new InvalidOperationException($"PR #{pr.Id} has no clone URL.");
        }

        progress?.Report($"Cloning {pr.Repository} (git clone)");
        var clone = await RunProcessAsync("git", [.. authArgs, "clone", pr.CloneUrl, repoDir], null, ct)
            .ConfigureAwait(false);
        ThrowIfFailed("git clone", clone);
    }

    private static async Task CheckoutSourceBranchAsync(
        PullRequestItem pr, string repoDir, IProgress<string>? progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pr.SourceBranch))
        {
            throw new InvalidOperationException($"PR #{pr.Id} has no source branch.");
        }

        // Point the local branch at the freshly-fetched remote tip, creating it if needed. This
        // updates an already-checked-out branch to the PR's latest commits (a pull), and works
        // for a fresh clone too.
        progress?.Report($"Checking out {pr.SourceBranch} at origin's latest (git checkout -B)");
        var origin = $"origin/{pr.SourceBranch}";
        var checkout = await RunProcessAsync("git", ["-C", repoDir, "checkout", "-B", pr.SourceBranch, origin], null, ct)
            .ConfigureAwait(false);
        ThrowIfFailed("git checkout", checkout);
    }

    private async Task<string> RunClaudeAsync(string prompt, string repoDir, IProgress<string>? progress, CancellationToken ct)
    {
        progress?.Report($"Running Claude Code review with model '{_options.ReviewModelId}' — this can take a while…");

        string[] args =
        [
            "--model", _options.ReviewModelId,
            "-p", prompt,
            "--output-format", "text",
            "--allowedTools", ALLOWED_TOOLS,
        ];

        var result = await RunProcessAsync(ClaudeExe, args, repoDir, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            LogClaudeStdErr(result.StdErr);
        }

        ThrowIfFailed("claude", result);
        return result.StdOut;
    }

    private string BuildPrompt(PullRequestItem pr, string repoDir, string? agentFilePath, IProgress<string>? progress)
    {
        var template = ReadAgentTemplate(repoDir, agentFilePath, progress);
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"Review this Azure DevOps pull request.\n\n");
        builder.Append(CultureInfo.InvariantCulture, $"Repository: {pr.Repository}\n");
        builder.Append(CultureInfo.InvariantCulture, $"Project: {pr.ProjectName}\n");
        builder.Append(CultureInfo.InvariantCulture, $"Title: {pr.Title}\n");
        builder.Append(CultureInfo.InvariantCulture, $"Branch: {pr.SourceBranch} -> {pr.TargetBranch}\n\n");
        builder.Append(template);
        return builder.ToString();
    }

    /// <summary>
    /// Returns the review-agent template, seeding a copy into the repo root (if absent) so a
    /// human inspecting the clone can see the instructions. See <see cref="ResolveAgentTemplate"/>
    /// for the resolution order.
    /// </summary>
    private string ReadAgentTemplate(string repoDir, string? agentFilePath, IProgress<string>? progress)
    {
        var template = ResolveAgentTemplate(agentFilePath, progress);

        try
        {
            var inRepo = Path.Combine(repoDir, "review-agent.md");
            if (!File.Exists(inRepo))
            {
                File.WriteAllText(inRepo, template);
            }
        }
        catch (IOException ex)
        {
            LogSeedFailure(ex);
        }

        return template;
    }

    /// <summary>
    /// Resolves the review-agent template with the following priority: the per-call
    /// <paramref name="agentFilePath"/> override, then the configured
    /// <see cref="WorkspaceOptions.ReviewAgentPath"/>, then the seeded built-in default at
    /// <see cref="AppPaths.ReviewAgentPath"/>, then the hardcoded <see cref="DEFAULT_AGENT_TEMPLATE"/>.
    /// A configured/override path that doesn't resolve to an existing file is skipped (with a
    /// warning reported through <paramref name="progress"/>) rather than failing the review.
    /// </summary>
    private string ResolveAgentTemplate(string? agentFilePath, IProgress<string>? progress)
    {
        if (!string.IsNullOrWhiteSpace(agentFilePath))
        {
            if (File.Exists(agentFilePath))
            {
                progress?.Report($"Using review agent override: {agentFilePath}");
                return File.ReadAllText(agentFilePath);
            }

            progress?.Report($"Review agent override '{agentFilePath}' was not found — falling back to the configured/default agent.");
        }

        if (!string.IsNullOrWhiteSpace(_options.ReviewAgentPath))
        {
            if (File.Exists(_options.ReviewAgentPath))
            {
                progress?.Report($"Using configured review agent: {_options.ReviewAgentPath}");
                return File.ReadAllText(_options.ReviewAgentPath);
            }

            progress?.Report($"Configured review agent '{_options.ReviewAgentPath}' was not found — falling back to the built-in default.");
        }

        return File.Exists(AppPaths.ReviewAgentPath) ? File.ReadAllText(AppPaths.ReviewAgentPath) : DEFAULT_AGENT_TEMPLATE;
    }

    private string? CheckWorkFolder()
    {
        if (string.IsNullOrWhiteSpace(_options.WorkFolderPath))
        {
            return "Work folder is not set. Configure it in Settings → Preferences → Workspace.";
        }

        try
        {
            Directory.CreateDirectory(_options.WorkFolderPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"Work folder '{_options.WorkFolderPath}' is not usable: {ex.Message}";
        }

        return null;
    }

    private static string[] BuildGitAuthArgs(string pat)
    {
        // Empty username + PAT as the password, Basic-encoded. Passed per-command via -c so it
        // is never written to the repo's .git/config (which the review agent can read).
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        return ["-c", $"http.extraHeader=Authorization: Basic {token}"];
    }

    private static async Task<bool> ToolRespondsAsync(string exe, string arg)
    {
        try
        {
            var result = await RunProcessAsync(exe, [arg], null, CancellationToken.None).ConfigureAwait(false);
            return result.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // Executable not found on PATH / at the configured location.
            return false;
        }
    }

    private static void ThrowIfFailed(string step, ProcessResult result)
    {
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
            throw new InvalidOperationException($"{step} failed (exit {result.ExitCode}). {detail}".Trim());
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string exe, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct)
    {
        var info = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = info };
        process.Start();

        // On cancellation, terminate the whole child tree (git/claude may spawn helpers) so a
        // cancelled review never leaves an orphaned process running.
        using var registration = ct.Register(static state => TryKill((Process)state!), process);

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdOut = await stdOutTask.ConfigureAwait(false);
        var stdErr = await stdErrTask.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, stdOut, stdErr);
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
            // Process already exited between the HasExited check and Kill.
        }
        catch (Win32Exception)
        {
            // The OS refused to terminate it; nothing more we can do.
        }
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        return builder.Length == 0 ? "repo" : builder.ToString();
    }

    private const string DEFAULT_AGENT_TEMPLATE =
        "You are a senior code reviewer. Analyse the pull request using the files in your current directory. " +
        "Diff the source branch against the target branch (use `git diff` as needed). " +
        "Output a Markdown report with these sections: Summary, Changed Files, Issues Found (a table with " +
        "Severity/File/Line/Issue), Suggestions, and Verdict (Approved / Needs Changes / Rejected).";

    private readonly record struct ProcessResult(int ExitCode, string StdOut, string StdErr);
}
