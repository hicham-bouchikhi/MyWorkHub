# Architecture Decision Records

This directory contains ADRs (Architecture Decision Records) for MyWorkHub.
Each ADR documents a significant technical decision: the context that forced it,
what was chosen, and what the ongoing consequences are.

ADRs are **append-only**. Once accepted, an ADR is never edited to change its
decision — a new ADR supersedes it instead.

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](0001-azdo-work-item-comments-preview-api.md) | AzDO work item comments endpoint is preview-only | Accepted |
| [0002](0002-azdo-rest-not-sdk.md) | Use raw HttpClient REST for Azure DevOps, not the TFS Client SDK | Accepted |
| [0003](0003-feature-module-architecture.md) | Vertical feature-slice modules discovered by reflection, not flat registration files | Accepted |
| [0004](0004-element-level-deep-linking.md) | Element-level deep-linking via NavigationTarget + IDeepLinkTarget, not a generic event bus | Accepted |
| [0005](0005-cli-agent-runner-is-claude-specific.md) | ICliAgentRunner is a Claude Code abstraction, not a provider-agnostic agent-CLI interface | Accepted |
| [0006](0006-scoped-live-settings-reload.md) | Scoped live-reload for edited settings via LiveOptions&lt;T&gt; and a polling file provider | Accepted |

## Format

Each ADR follows the [Nygard style](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions):
**Context → Decision → Consequences.**
