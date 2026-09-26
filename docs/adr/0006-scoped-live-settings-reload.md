# ADR-0006: Scoped live-reload for edited settings via LiveOptions&lt;T&gt; and a polling file provider

**Status:** Accepted
**Date:** 2026-09-25

## Context

Before the Settings page existed, every `XxxOptions.FromConfiguration(IConfiguration)` record (Email,
Azure DevOps, Workspace, ...) was read exactly once, at DI-registration time, and baked into a singleton.
Once the Settings page needed to let a user edit the Azure DevOps organization/projects and the
Workspace (PR-review clone location, Claude CLI path, review model/agent) and have those changes take
effect immediately, this baked-singleton pattern had to change for those specific settings.

Two broader alternatives were considered and rejected:

1. **`Microsoft.Extensions.Options`' `IOptionsMonitor<T>`** with the reflection-based configuration
   binder. Rejected because every `XxxOptions.FromConfiguration` method deliberately reads its section
   key-by-key rather than through the reflection binder (for AOT/trim-analyzer cleanliness — `Core` and
   `Presentation` opt into `IsAotCompatible=true`), and `IOptionsMonitor<T>` is built around binder-driven
   `IConfigureOptions<T>` pipelines that don't compose naturally with that style.
2. **Making every options type in the app live-reloadable**, for consistency. Rejected as unnecessary
   scope: the Settings page only exposes Appearance, Workspace, and Azure DevOps for editing. `AzureAD`,
   `Email`, `Automation` (Quartz cron schedules), and `ExternalSites` are still read once at startup and
   are not editable from the UI at all, so making them live-reloadable would add risk (e.g. a Quartz job
   whose schedule silently changes mid-run) for a capability nothing exposes.

Separately, enabling `reloadOnChange: true` on the JSON configuration file needed a file-watch mechanism.
A plain `FileSystemWatcher` watches its target directory **recursively** by default; `~/.MyWorkHub` also
contains `repos/`, where PR review clones live, and Linux's per-user `inotify` watch limit is a real
constraint — one clone can contain thousands of subdirectories, each consuming a watch.

## Decision

- Introduce `LiveOptions<T>(Func<T> read)`, whose `Current` property re-invokes the factory (i.e.
  `XxxOptions.FromConfiguration(configuration)`) on every access. Register it in place of a baked
  singleton **only** for `AzureDevOpsOptions` and `WorkspaceOptions` — the two option types the Settings
  page actually lets the user edit. Consumers (`AzureDevOpsClient`, `AzureDevOpsService`,
  `ClaudeCliAgentRunner`, `ClaudePrReviewService`, `GitRepositoryWorkspace`) take `LiveOptions<T>` and
  call `.Current` once per operation instead of caching a record field.
- Enable `reloadOnChange: true` via a `PhysicalFileProvider` configured with `UsePollingFileWatcher = true`
  and `UseActivePolling = true` — polling instead of `FileSystemWatcher`, specifically to avoid the
  recursive-inotify-exhaustion risk under `repos/`.
- `Appearance` (theme/palette) doesn't go through `LiveOptions<T>` at all — it applies immediately via the
  existing `IThemeService` seam and is saved separately; there's no intermediate options record to make
  live in the first place.
- Settings not exposed in the UI (`AzureAD`, `Email`, `Automation`, `ExternalSites`,
  `UI:RefreshIntervalMinutes`, `UI:MinimizeToTrayOnClose`) remain read-once-at-startup. This is documented
  in CLAUDE.md, not hidden.

## Consequences

- Saving Azure DevOps or Workspace settings takes effect on the next operation against the same running
  service instances, with no DI re-registration and no app restart — this is what actually fixes the
  "Could not reach Azure DevOps" problem the Settings page was built to solve: there was previously no UI
  to set the organization/projects at all.
- The scope is intentionally narrow. Adding a new Settings-editable value means explicitly wrapping its
  options type in `LiveOptions<T>` (or adding a UI note that it needs a restart) — it is not automatic
  just because a value is read from `IConfiguration`.
- The polling file provider means a hand-edit to `appsettings.json` is picked up within the polling
  interval, not instantly — acceptable for a single-user desktop app's config file, and it sidesteps the
  inotify-exhaustion failure mode entirely rather than needing to exclude `repos/` from a recursive watch.
