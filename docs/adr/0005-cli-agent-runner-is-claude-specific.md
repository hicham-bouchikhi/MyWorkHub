# ADR-0005: ICliAgentRunner is a Claude Code abstraction, not a provider-agnostic agent-CLI interface

**Status:** Accepted
**Date:** 2026-09-25

## Context

Two AI features — the email digest and PR review — both shell out to a locally installed CLI and share
identical security requirements (system prompt passed as a CLI argument, never over stdin; the untrusted
user message the other way around; never run in `--bare` mode; run in an isolated working directory).
Pulling this into one abstraction, `ICliAgentRunner`, was correct and deliberate — the alternative
(each feature reimplementing its own process-launch/argument-building logic) is exactly the kind of
duplicated-security-logic that tends to drift out of sync.

The interface name and shape, however, invite an assumption the code does not actually deliver on: that
`ICliAgentRunner` is a provider-agnostic seam a second implementation (Codex CLI, Gemini CLI, another
agent tool) could plug into without touching Core. It is not. `CliAgentRequest`'s fields —
`StrictMcpConfig`, `DisallowedTools`, `AllowedTools` — are Claude Code's own CLI flag names
(`--strict-mcp-config`, `--disallowedTools`, `--allowedTools`) lifted directly into the shared Core
record, and the interface's own doc comment names the `claude` binary explicitly. There is exactly one
implementation, `ClaudeCliAgentRunner`, and the app's stated scope (per CLAUDE.md) has always been "drives
the locally installed Claude CLI," not "drives a pluggable agent CLI."

## Decision

Keep `ICliAgentRunner` as a **Claude-Code-specific** abstraction. It exists to:

1. Give the two consuming features (`ClaudeEmailSummaryService`, `ClaudePrReviewService`) a seam to
   depend on instead of calling `Process.Start` directly, so they're unit-testable against a fake.
2. Centralize the security-critical argument-building rules in one place instead of duplicating them.

It does **not** attempt to be a lowest-common-denominator abstraction over "any AI agent CLI." If a
second CLI backend is ever needed, expect to introduce a new, differently-shaped interface (or
substantially reshape `CliAgentRequest`) rather than assuming today's shape already accommodates it —
Claude Code's tool-permission model (`--disallowedTools`/`--allowedTools`/`--strict-mcp-config`) is not
guaranteed to have an equivalent in another CLI's flag set.

## Consequences

- The security invariants (system prompt as argument, user message on stdin, temp-directory isolation,
  never `--bare`) are enforced in exactly one place and covered by tests that assert on the literal
  argument list `ClaudeCliAgentRunner` builds.
- Anyone reading `ICliAgentRunner` in isolation could reasonably assume it's provider-neutral; this ADR
  is the record that it deliberately is not, so a future attempt to bolt on a second implementation
  should treat it as a design question (reshape the interface first) rather than a drop-in.
- If Anthropic changes the `claude` CLI's flag surface, `ClaudeCliAgentRunner.BuildArguments` is the one
  place to update; `CliAgentRequest`'s field names would likely need to change too, since they mirror the
  CLI's vocabulary directly rather than an abstracted concept.
