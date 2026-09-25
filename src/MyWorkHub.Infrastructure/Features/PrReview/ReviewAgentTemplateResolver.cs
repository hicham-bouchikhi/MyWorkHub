namespace MyWorkHub.Infrastructure.Features.PrReview;

/// <summary>
/// Picks the review-agent Markdown template (the system prompt of a pull request review). Resolution order:
/// <list type="number">
/// <item>the per-review override chosen for one pull request;</item>
/// <item>the configured global template (<c>Workspace:ReviewAgentPath</c>);</item>
/// <item>the built-in template seeded to <c>~/.MyWorkHub/review-agent.md</c> on first run;</item>
/// <item><see cref="DEFAULT_AGENT_TEMPLATE"/>, compiled in.</item>
/// </list>
/// A user-supplied path (1 or 2) that is missing, unreadable or empty is skipped with a progress warning rather
/// than failing the review. A missing seeded copy (3) is skipped silently — it is not something the user chose.
/// </summary>
internal sealed class ReviewAgentTemplateResolver
{
    /// <summary>Last-resort template, kept in sync with the shipped <c>review-agent.md</c>.</summary>
    internal const string DEFAULT_AGENT_TEMPLATE =
        "You are a senior code reviewer. Analyse the pull request using the files in your current directory.\n" +
        "Diff the source branch against the target branch (use `git diff` as needed).\n\n" +
        "Output a Markdown report with exactly these sections:\n\n" +
        "## Summary\nOne-paragraph overview of the change.\n\n" +
        "## Changed Files\nEach changed file with a one-line description.\n\n" +
        "## Issues Found\n| Severity | File | Line | Issue |\n|----------|------|------|-------|\n" +
        "Use High / Medium / Low. If none, write \"No issues found.\"\n\n" +
        "## Suggestions\nBullet list of concrete improvements.\n\n" +
        "## Verdict\nApproved / Needs Changes / Rejected — one sentence.\n";

    private readonly Func<string?> _configuredPath;
    private readonly string _seededDefaultPath;

    /// <param name="configuredPath">Reads the user's global template (<c>Workspace:ReviewAgentPath</c>, or null)
    /// on every resolution, so a template changed in Settings applies to the next review.</param>
    /// <param name="seededDefaultPath">Where the built-in template is seeded (<c>AppPaths.ReviewAgentPath</c>).</param>
    public ReviewAgentTemplateResolver(Func<string?> configuredPath, string seededDefaultPath)
    {
        ArgumentNullException.ThrowIfNull(configuredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(seededDefaultPath);
        _configuredPath = configuredPath;
        _seededDefaultPath = seededDefaultPath;
    }

    /// <summary>Returns the template text to use, reporting which source was picked (or skipped) to <paramref name="progress"/>.</summary>
    public string Resolve(string? overridePath, IProgress<string>? progress = null)
    {
        if (TryUserTemplate(overridePath, "per-review", progress) is { } fromOverride)
        {
            return fromOverride;
        }

        if (TryUserTemplate(_configuredPath(), "configured", progress) is { } fromConfiguration)
        {
            return fromConfiguration;
        }

        if (TryRead(_seededDefaultPath) is { } seeded)
        {
            progress?.Report($"Using the built-in review agent ({_seededDefaultPath}).");
            return seeded;
        }

        progress?.Report("Using the compiled-in default review agent.");
        return DEFAULT_AGENT_TEMPLATE;
    }

    private static string? TryUserTemplate(string? path, string kind, IProgress<string>? progress)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (TryRead(path) is { } template)
        {
            progress?.Report($"Using the {kind} review agent ({path}).");
            return template;
        }

        progress?.Report($"Warning: the {kind} review agent '{path}' is missing, unreadable or empty; falling back.");
        return null;
    }

    private static string? TryRead(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
