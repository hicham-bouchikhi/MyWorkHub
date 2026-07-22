namespace MyWorkHub.Infrastructure.Ai;

/// <summary>
/// Builds the command-line arguments for the locked-down Claude CLI summarisation run. The task
/// is pure text summarisation of untrusted email content, so the CLI is stripped of every tool
/// and ambient config as defense-in-depth against prompt injection: even if the model is coerced
/// by injected text, it can neither execute commands, mutate files, reach the network, nor spawn
/// sub-agents. Auth still flows through the CLI's own logged-in session, so <c>--bare</c> (which
/// disables OAuth/keychain and demands an API key) is deliberately NOT used.
/// </summary>
public static class ClaudeCliArguments
{
    // Tools denied for the summariser — it needs none. Covers execution (Bash), mutation
    // (Edit/Write/NotebookEdit), network egress (WebFetch/WebSearch), and delegation (Task).
    private const string DENIED_TOOLS = "Bash,Edit,Write,NotebookEdit,WebFetch,WebSearch,Task";

    /// <summary>
    /// Arguments for <c>claude</c>: headless print mode, plain-text output, the trusted
    /// instructions as the (replaced) system prompt, all dangerous tools denied, and ambient
    /// MCP servers ignored. The untrusted email content is fed separately over stdin.
    /// </summary>
    public static IReadOnlyList<string> ForSummary(string systemPrompt)
    {
        ArgumentNullException.ThrowIfNull(systemPrompt);
        return
        [
            "-p",
            "--output-format", "text",
            "--system-prompt", systemPrompt,
            "--disallowedTools", DENIED_TOOLS,
            "--strict-mcp-config",
        ];
    }
}
