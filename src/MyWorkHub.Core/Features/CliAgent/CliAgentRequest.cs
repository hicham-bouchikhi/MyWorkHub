namespace MyWorkHub.Core.Features.CliAgent;

/// <summary>One headless run of the agent CLI (see <see cref="ICliAgentRunner"/>).</summary>
/// <param name="SystemPrompt">Trusted instructions; replaces the CLI's default system prompt. Passed as a
/// command-line argument, never over standard input.</param>
/// <param name="UserMessage">The task and its data, fed over standard input. May carry untrusted content —
/// quarantine it inside the message (see the email digest's nonce-delimited block).</param>
/// <param name="WorkingDirectory">Directory the CLI runs in (and whose files it can see); <c>null</c> runs it
/// in a fresh empty temporary directory that is deleted afterwards.</param>
/// <param name="ModelId">Model to use; <c>null</c> keeps the CLI's own default.</param>
/// <param name="DisallowedTools">Tools the agent must never use (e.g. <c>Bash</c>, <c>WebFetch</c>).</param>
/// <param name="StrictMcpConfig">When true, every ambient MCP server configuration is ignored (none is
/// supplied), so the run gets no MCP tools at all.</param>
/// <param name="AllowedTools">Tools pre-approved to run without a permission prompt (a headless run cannot
/// answer one), e.g. <c>Read</c> or <c>Bash(git diff:*)</c>.</param>
public sealed record CliAgentRequest(
    string SystemPrompt,
    string UserMessage,
    string? WorkingDirectory = null,
    string? ModelId = null,
    IReadOnlyList<string>? DisallowedTools = null,
    bool StrictMcpConfig = true,
    IReadOnlyList<string>? AllowedTools = null);
