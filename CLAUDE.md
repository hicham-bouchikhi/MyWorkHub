# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

MyWorkHub is a single-user **Windows desktop app** (Avalonia UI 12 + .NET 10) that consolidates one engineer's daily Cegid workflow: Outlook mail/calendar, Teams chats, Azure DevOps PRs/work items, a local todo list, and scheduled browser automations (transport reimbursement, remote-work day sync to PeopleNet/mwork/Outlook). It also uses the **Claude CLI** for AI-assisted features: email digest summarisation and PR code review. It is **not** a shipped library — keep the design oriented to a local, single-user tool.

`docs/prd.md`, `docs/architecture.md`, and `docs/tasks.md` are the authoritative specs. `docs/architecture.md` is detailed and current — **read it before scaffolding new services**; §6 fixes the exact interface signatures, §7 the data models, and §12 lists implementation choices that are easy to get wrong. Several interfaces and models have been extended since it was written — see the corrections below.

## Build & test

The solution is `MyWorkHub.slnx` (XML solution format — note: not a classic `.sln`).

```bash
# Solution-wide build/test MUST be single-threaded — parallel restore crashes
# in NuGet.Frameworks on .slnx. Always pass -m:1 for whole-solution commands.
dotnet build MyWorkHub.slnx -m:1
dotnet test  MyWorkHub.slnx -m:1

# Per-project commands run fine without -m:1:
dotnet build src/MyWorkHub.App/MyWorkHub.App.csproj
dotnet test  tests/MyWorkHub.UI.Tests/MyWorkHub.UI.Tests.csproj

# Run the app
dotnet run --project src/MyWorkHub.App
```

Tests use **xUnit v3** on the classic VSTest runner (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`), so standard `dotnet test` filtering applies. There are three test projects:

```bash
# ViewModel / navigation tests (net10.0)
dotnet test tests/MyWorkHub.UI.Tests/MyWorkHub.UI.Tests.csproj --filter "FullyQualifiedName~DashboardViewModelTests"

# Graph mappers, MSAL, AzDO REST, EF Core repos (net10.0 — cross-platform)
dotnet test tests/MyWorkHub.Infrastructure.Tests/MyWorkHub.Infrastructure.Tests.csproj

# AppPaths and other pure-Core logic (net10.0)
dotnet test tests/MyWorkHub.Core.Tests/MyWorkHub.Core.Tests.csproj
```

## Zero-warning / analyzer policy

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, and `AnalysisMode=All`. **The build fails on any warning** — fix the cause, do not suppress globally. Per-rule relaxations live in `.editorconfig` and each carries a written justification; if you need to turn a rule off, add it there with rationale, matching that style. The `dotnet-tdd` skill governs C# work here (red-green-refactor, zero warnings) — follow it.

**AOT is intentionally NOT enabled** (EF Core, Graph, MSAL, Playwright, Quartz are all reflection-based). But the IL2026/IL2091/IL3050/IL3051 trim/AOT analyzers are kept as **hard errors** as a tripwire — don't introduce code that trips them.

House naming style (enforced as warnings → errors): constants are `UPPER_CASE_WITH_UNDERSCORES`, private fields are `_camelCase`, test methods use `Should_do_X_when_Y` underscores.

## Architecture

Strict layered dependency flow — **UI never touches Infrastructure directly**; everything goes UI → Core interfaces ← Infrastructure.

| Project | TFM | Role |
|---------|-----|------|
| `MyWorkHub.App` | `net10.0` | Entry point, Avalonia bootstrap, DI composition root (`CompositionRoot.cs`), `appsettings.json` load, Quartz startup. Cross-platform (Win/Linux/macOS); the Win32 `app.manifest` is applied only on Windows builds |
| `MyWorkHub.UI` | `net10.0` | Avalonia Views (`.axaml`) + ViewModels (CommunityToolkit.Mvvm `ObservableObject`). References Core only |
| `MyWorkHub.Core` | `net10.0` | Domain models, DTOs, **all service interfaces**, infrastructure-free business logic. Pure C# — no Avalonia/HTTP/DB |
| `MyWorkHub.Infrastructure` | `net10.0` | Implementations: Graph (email/calendar/Teams), Azure DevOps, Playwright, EF Core + SQLite, Data Protection credential store. Cross-platform |
| `MyWorkHub.Automation` | `net10.0` | Quartz.NET `IJob` workflow orchestrators. References Core interfaces only |

DI is wired in two places: `CompositionRoot.cs` (App) composes everything; `InfrastructureServiceCollectionExtensions.cs` registers the infrastructure implementations. Register `ICredentialStore` (DPAPI) **before** anything that consumes credentials (MSAL, AzDO client).

**ViewLocator convention**: `ViewLocator` maps any `XxxViewModel` to `XxxView` by replacing the `ViewModel` suffix with `View` in the fully-qualified type name. Both classes must live in matching namespaces (e.g., `MyWorkHub.UI.ViewModels.EmailViewModel` → `MyWorkHub.UI.Views.EmailView`). Breaking this naming breaks navigation silently (shows "Not Found:" text block).

**Optional service injection pattern**: ViewModels that aggregate multiple data sources (e.g., `DashboardViewModel`) take their services as nullable optional constructor parameters (`IEmailService? email = null`). This lets them degrade gracefully when a service is unavailable or not yet configured, rather than throwing at resolution time. Follow this pattern for any new ViewModel that touches an optional data source.

**appsettings.json seeding**: The app reads config from `%USERPROFILE%\.MyWorkHub\appsettings.json`, not from the embedded `src/MyWorkHub.App/appsettings.json`. On first launch, `CompositionRoot.SeedUserConfig()` copies the embedded file to the user profile if it does not already exist. To change runtime config (AzureAD tenant, AzDO org URL, UI theme/palette, cron expressions), edit the one in `~/.MyWorkHub/`.

### Interface and model corrections (post-ARCHITECTURE.md changes)

docs/architecture.md §6 and §7 are the canonical starting point, but these items have diverged:

**`IEmailService`** — two additional methods not in §6:
```csharp
Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default);
Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default);
```

**`IAzureDevOpsService`** — one additional method not in §6:
```csharp
Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
    IReadOnlyList<WorkItem> workItems, CancellationToken ct = default);
```

**`WorkItem` record** — two trailing defaulted parameters added (non-breaking):
```csharp
public record WorkItem(int Id, string Title, string Type, string State, string Priority,
    DateTime? DueDate, string Url, string Effort = "", string Project = "");
```
`Project` is required for `GetWorkItemMentionsAsync` — comments are queried per project.

**`EmailItem` record** — one additional positional parameter and one init property:
```csharp
public record EmailItem(string Id, string From, string Subject, string Preview,
    DateTime ReceivedAt, bool IsFlagged, string FolderName = "")
{
    public string? FullBody { get; init; }  // fetched lazily before summarisation
}
```

**`IPrReviewService.ReviewAsync`** — one additional optional parameter, inserted before `ct` (analyzers require `CancellationToken` last):
```csharp
Task<string> ReviewAsync(PullRequestItem pr, IProgress<string>? progress = null,
    string? agentFilePath = null, CancellationToken ct = default);
```
`agentFilePath` is a one-off, per-call override of the review-agent Markdown template (e.g. a
different `.md` chosen for a single PR from the Pull Requests list). Resolution order in
`ClaudeCodePrReviewService.ResolveAgentTemplate`: `agentFilePath` override → configured
`WorkspaceOptions.ReviewAgentPath` → seeded built-in default (`AppPaths.ReviewAgentPath`) →
hardcoded `DEFAULT_AGENT_TEMPLATE` constant. A configured/override path that doesn't resolve is
skipped (with a `progress` warning) rather than failing the review.

**`WorkspaceSettings` record / `IWorkspaceSettingsService.Save`** — one additional field/parameter:
```csharp
public sealed record WorkspaceSettings(
    string WorkFolderPath, string ClaudeExecutablePath, string ReviewModelId, string ReviewAgentPath);

void Save(string workFolderPath, string claudeExecutablePath, string reviewModelId, string reviewAgentPath);
```
`ReviewAgentPath` is the user-configured global review-agent Markdown file; empty means "use the
built-in default." Settings UI: Settings → Preferences → Code review → "Review agent" (Browse/Reset).

**New interfaces** (not in ARCHITECTURE.md):
- `IEmailSummaryService` — AI digest of a list of `EmailItem` via Claude CLI
- `IEmailSettingsService` — reads/saves watched mail folder IDs (to `appsettings.json`, live update)
- `IPrReviewService` — Claude Code PR review: clone/fetch repo, checkout branch, run `claude`, return HTML report path
- `IWorkspaceSettingsService` — reads/saves Claude CLI path + work folder + review model ID + review agent path (to `appsettings.json`, live update)
- `ISeenMentionRepository` — persists which AzDO comment IDs the user has already opened (`SeenMentions` SQLite table, PK = `CommentId`)
- `IFilePicker` — cross-platform single-file chooser (mirrors `IFolderPicker`), implemented as `AvaloniaFilePicker` over `IStorageProvider.OpenFilePickerAsync`; used for browsing to a review-agent `.md` file, globally in Settings or per-PR from the Pull Requests list

**New DTO**: `WorkItemMention(int WorkItemId, string WorkItemTitle, string WorkItemUrl, int CommentId, string AuthorDisplayName, DateTime CreatedAt, string TextSnippet)`

**New EF entity**: `SeenWorkItemMention` — PK is `CommentId` (int), `ValueGeneratedNever`.

**`CredentialKeys` static class**: Use `CredentialKeys.AZURE_DEVOPS_PAT`, `CredentialKeys.TCL_USER`, etc. from `MyWorkHub.Core.Abstractions.CredentialKeys` instead of raw string literals when calling `ICredentialStore`.

### Integration specifics that bite

- **Azure DevOps uses raw HttpClient REST, NOT the TFS Client SDK** — the SDK pulls in a vulnerable SqlClient transitive dep and bloat. Keep AzDO on REST.
- **Microsoft Graph is 6.x** (6.2.0, Kiota-based) despite docs/architecture.md §5 saying "5.x" — tests use the `GraphServiceClient(IRequestAdapter)` seam.
- **SQLite**: uses `Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3` (cross-platform native bundle). The bundled-`e_sqlite3` advisory `GHSA-2m69-gcr7-jv3q` is suppressed in `Directory.Build.props` with rationale (single-user local DB, never from an untrusted source).
- **Credential store is cross-platform**: `DataProtectionCredentialStore` uses `Microsoft.AspNetCore.DataProtection` (DPAPI on Windows, AES-256+HMAC key ring on Linux/macOS), **not** the old Windows-only DPAPI `ProtectedData`. The key ring lives at `~/.MyWorkHub/keys/`. Register it via `AddDataProtection().PersistKeysToFileSystem(...).SetApplicationName("MyWorkHub")` before anything that consumes credentials.
- **MSAL**: always `WithUseEmbeddedWebView(false)` (system browser for Cegid SSO). Token cache at `~/.MyWorkHub/msal_token_cache.bin` (encrypted per-OS by `MsalCacheHelper`).
- **Playwright**: Chromium only. Register the download handler **before** clicking; `DownloadFileAsync` writes to disk and returns a path (never `byte[]`).
- **EF Core**: migrate via `dbContext.Database.MigrateAsync()` on startup — never require manual `dotnet ef`.
- **`RemoteWorkSyncJob`**: the 3 sync steps (Outlook, PeopleNet, mwork) each get their own `try/catch` — one failure must not abort the others. This is why `CA1031` (broad catch) is disabled.
- **`IBrowserLauncher` ≠ `IBrowserService`**: `IBrowserLauncher` is a UI-layer service (`BrowserLauncher`, shell-execute) registered in `AddUi()` that opens a URL in the default browser. It is **not** Playwright. `IBrowserService` / `IBrowserSession` is the Playwright wrapper in Infrastructure. Do not confuse them.
- **AzDO comment @mentions use a preview-only API** — `_apis/wit/workItems/{id}/comments` has been at `7.1-preview.3` since at least API v5.1 and has **never been promoted to GA** (Microsoft's own SDK still marks it `[Preview API]`). It is called best-effort: each item has its own `try/catch`, and the whole mentions block in `WorkItemsViewModel.RefreshAsync` is also `try/catch`-wrapped — a 400/404 just hides the Mentions section without breaking work items. If Microsoft ever promotes it to GA, update `COMMENTS_API_VERSION` in `AzureDevOpsService.cs` (one constant). A 12-week deprecation window is guaranteed before any preview version is deactivated. Full rationale in **`docs/adr/0001-azdo-work-item-comments-preview-api.md`**. Mention detection: `WorkItemMentionScanner.ContainsMentionOf` strips HTML via `HtmlBodyExtractor.StripTags` and checks for `@{displayName}` (full-name, case-insensitive). Read-state in `SeenMentions` (SQLite, PK = `CommentId`) via `ISeenMentionRepository`.
- **`NotificationCenterViewModel` is a singleton** shared between `ToastNotificationService` (writes notifications) and the shell. Use `Add()` to push a notification entry; use `BeginActivity`/`EndActivity` for live in-progress operations that show in the bell flyout with a Stop button. Capped at 200 history entries.
- Runtime data lives under `%USERPROFILE%\.MyWorkHub\` (SQLite db, appsettings, logs, temp downloads, error screenshots). Sensitive values (passwords, PATs) never go in `appsettings.json` — only DPAPI via `ICredentialStore`, referenced by key.

### Library/API docs

Use the `find-docs` skill / `ctx7` CLI for current docs on Avalonia, Microsoft Graph, MSAL, EF Core, Quartz, Playwright, CommunityToolkit.Mvvm before relying on memory — these APIs move fast.

## AI features (Claude CLI)

Both the email digest summary and PR code review delegate to the `claude` binary already installed on the user's PATH (or configured at `Workspace.ClaudeExecutablePath` in `appsettings.json`).

**Key design rules:**
- Run `claude` in an **empty temp subdirectory** (`Directory.CreateTempSubdirectory`) — no project `CLAUDE.md` or repo files must leak into the prompt context.
- **Never use `--bare`** — it disables OAuth/keychain and breaks the user's existing `claude` login session.
- Pass the system prompt via `--system-prompt` (CLI argument), never via stdin, so attacker-controlled content cannot override instructions.
- For the email summariser, all dangerous tools are denied (`--disallowedTools Bash,Edit,Write,NotebookEdit,WebFetch,WebSearch,Task`) and ambient MCP is disabled (`--strict-mcp-config`).
- Email content is quarantined inside a **nonce-delimited block** (random 16-byte hex boundary per call, via `EmailSummaryPrompt.BuildUserMessage`) — the nonce prevents email text from forging the closing marker to escape the block.
- PR review uses `src/MyWorkHub.App/review-agent.md` as the default instruction file (seeded to `AppPaths.ReviewAgentPath`); `ClaudeCodePrReviewService` clones/fetches the repo into `WorkFolderPath` and checks out the source branch before invoking `claude`. A user-configured `Workspace.ReviewAgentPath` or a per-PR override takes priority over this default — see the `IPrReviewService.ReviewAsync` correction above.

**`appsettings.json` sections added after docs/architecture.md §10:**

```json
"Workspace": {
  "WorkFolderPath": "",
  "ClaudeExecutablePath": "",
  "ReviewModelId": "claude-sonnet-5",
  "ReviewAgentPath": ""
},
"Email": {
  "FolderIds": [ "inbox" ],
  "MaxPerFolder": 25
}
```
