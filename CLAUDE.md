# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

MyWorkHub is a single-user **desktop app** (Avalonia UI 12 + .NET 10; Windows first, also runs on Linux/macOS) that consolidates one engineer's daily Cegid workflow: Outlook mail/calendar, Teams chats, Azure DevOps PRs/work items, a local todo list, and scheduled browser automations (transport reimbursement, remote-work day sync to Outlook/PeopleNet/mwork). It also drives the locally installed **Claude CLI** for two AI features: an email digest and PR code review. It is **not** a shipped library — keep the design oriented to a local, single-user tool.

**The code is the source of truth.** `docs/prd.md`, `docs/architecture.md` and `docs/tasks.md` are the pre-rewrite specs: still useful for product intent, but their interface signatures, project layout and registration model predate the feature-module rewrite described below — where they disagree with the code, the code wins. `docs/adr/` records decisions that still hold (AzDO comments preview API, AzDO REST not SDK).

## Build & test

The solution is `MyWorkHub.slnx` (XML solution format — not a classic `.sln`).

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

Tests use **xUnit v3** on the classic VSTest runner (`Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`), so standard `dotnet test --filter` applies. Four test projects, each mirroring the `Features/<Name>/` layout of what it tests:

| Project | Covers |
|---|---|
| `tests/MyWorkHub.Core.Tests` | Pure Core logic: `ModuleDiscovery`, navigation records, `AppPaths`, `CredentialKeys`, automation plan/outcome rules |
| `tests/MyWorkHub.Infrastructure.Tests` | Graph services (fake Kiota `IRequestAdapter`), MSAL, AzDO REST (stub `HttpMessageHandler`), EF Core repos (in-memory SQLite), Claude CLI runner / prompts, Playwright sessions (fake browser), process runner |
| `tests/MyWorkHub.UI.Tests` | Page view models, navigation + deep links, notification center, and the real `AddUi()` composition (`ShellCompositionValidator`, `ViewRegistry`) |
| `tests/MyWorkHub.Automation.Tests` | Quartz jobs (`AutomationJob`, `RemoteWorkSyncJob`, …) and scheduling registration |

```bash
dotnet test tests/MyWorkHub.UI.Tests/MyWorkHub.UI.Tests.csproj --filter "FullyQualifiedName~TodoDeepLinkTests"
```

## Zero-warning / analyzer policy

`Directory.Build.props` sets `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, and `AnalysisMode=All`. **The build fails on any warning** — fix the cause, do not suppress globally. Per-rule relaxations live in `.editorconfig`, each with a written justification (e.g. CA1707 is off for `tests/**` and for `**/CredentialKeys.cs`); if you must turn a rule off, add it there with rationale in the same style. The `dotnet-tdd` skill governs C# work here (red-green-refactor, zero warnings) — follow it.

**AOT is intentionally NOT enabled** (EF Core, Graph, MSAL, Playwright, Quartz are reflection-based). `Core` and `Presentation` opt into `IsAotCompatible=true`, which makes the IL2026/IL2091/IL3050/IL3051 trim/AOT analyzers (errors via `.editorconfig`) a real tripwire there — don't introduce code that trips them. Reflection entry points (module discovery, `AddUi`, `AddInfrastructure`, `ViewLocator`) are marked `[RequiresUnreferencedCode]` and the attribute propagates to their callers.

House conventions (enforced as errors where an analyzer exists):
- Constants `UPPER_CASE_WITH_UNDERSCORES`; private fields `_camelCase`; test methods `Should_do_X_when_Y`.
- Enum members `UPPER_CASE` (e.g. `NotificationSeverity.INFORMATION`, `AutomationSite.PEOPLENET`).
- `CancellationToken ct` is always the last parameter.
- **Optional service injection**: page view models take every service as a nullable optional constructor parameter (`IEmailService? email = null`) and degrade gracefully (a notice instead of the list, disabled buttons) when one is absent or not configured — never throw at resolution time. Follow this for every new page.
- No dead code, no backwards-compat shims.

## Architecture

### Layers

Strict dependency flow — **UI never touches Infrastructure directly**; everything goes through Core interfaces. Only `App` (the composition root) references Infrastructure and Automation.

| Project | Role | References |
|---------|------|------------|
| `MyWorkHub.Core` | Domain records, service interfaces, module contracts, navigation records. Pure C# — no Avalonia/HTTP/DB | — |
| `MyWorkHub.Infrastructure` | Implementations: Graph (mail/calendar/Teams), MSAL, AzDO REST, EF Core + SQLite, Data Protection credential store, Claude CLI runner, Playwright | Core |
| `MyWorkHub.Presentation` | Framework-agnostic view models (CommunityToolkit.Mvvm), navigation service, notification center. No Avalonia | Core |
| `MyWorkHub.UI` | Avalonia views (`.axaml`), view modules, `ViewLocator`, shared behaviors, themes, toast notifications, pickers | Core, Presentation |
| `MyWorkHub.Automation` | Quartz.NET jobs + scheduling (`IAutomationModule`) | Core |
| `MyWorkHub.App` | Entry point, `CompositionRoot`, Avalonia `App`, tray, single-instance guard, Quartz start/stop | all |

### Vertical feature slices + module discovery (the most important fact in this repo)

A feature is **one folder per layer**, `Features/<Name>/`, and registers itself through a **module** that is discovered by reflection. There is no shared registration file to edit, no hardcoded sidebar list, and no naming-convention view lookup:

| Contract (in `Core/Modules`, except `IAutomationModule`) | Implemented in | Replaces |
|---|---|---|
| `IInfrastructureModule.RegisterServices(IServiceCollection, IConfiguration)` | `Infrastructure/Features/<Name>/<Name>InfrastructureModule.cs` | manual edits to `InfrastructureServiceCollectionExtensions` |
| `IPresentationModule { NavigationItem? MenuEntry; RegisterServices(IServiceCollection) }` | `Presentation/Features/<Name>/<Name>PresentationModule.cs` | manual edits to `AddPresentation()` and a hardcoded sidebar array |
| `IViewModule { IReadOnlyDictionary<Type,Type> ViewModelToView }` | `UI/Features/<Name>/<Name>ViewModule.cs` | the old string-convention `ViewLocator` |
| `IAutomationModule { AutomationDescriptor Automation; Type JobType }` (in `Automation/Scheduling`) | `Automation/Scheduling/IAutomationModule.cs` | manual Quartz wiring |

- `ModuleDiscovery.Find<T>(assemblies)` returns one instance of every non-abstract, public-parameterless-constructible `T` in the scanned assemblies (ordered by type name). Each layer's composition method calls it once: `AddInfrastructure()` scans Infrastructure, `AddUi()` scans Presentation + UI (for both presentation and view modules), `AddAutomations()` scans Automation. Module classes must be `public sealed` with a parameterless constructor.
- The sidebar is built only from discovered `MenuEntry`s, sorted by `NavigationItem.Order` then label. Current orders: Dashboard `int.MinValue`, Email 10, Calendar 20, Teams 30, Pull requests 40, Work items 50, Todo 70, Automations 80, Settings `int.MaxValue - 1`, Developer `int.MaxValue`. The shell lands on the first entry.
- `ViewRegistry` merges every `IViewModule` map (a view model mapped twice throws at composition); `ViewLocator` builds views from it and **throws** (naming the type) for an unmapped view model — there is no "Not Found:" placeholder any more.
- **Fail loud at startup**: after building the container, `CompositionRoot.Build()` migrates the database, then runs `ShellCompositionValidator.Validate`, which resolves every registered `PageViewModel` and every sidebar target and checks each has a constructible view (a concrete `Control` with a public parameterless constructor). Any gap aborts launch with a message listing every offending type. Consequence: **page view-model constructors run at startup** — keep them cheap, and remember they can touch the DB (that is why migration runs before validation).
- Features that share plumbing have their own non-page slices: `GraphAuth` (MSAL + `GraphServiceClient`, `GraphPageViewModel` base + `GraphPageStatus` sign-in panel), `AzureDevOps` (REST client, `AzureDevOpsPageViewModel` base + `AzureDevOpsPageStatus` PAT panel), `CliAgent`, `EmailSummary`, `PrReview`, `Workspace`. `Settings` is a page slice (see Configuration below).

#### Recipe: adding a new feature (copy the Todo slice)

`Todo` is the reference template — the simplest complete slice (local EF Core, no external API, deep-linkable list). For a new feature `Foo`:

1. **Core** — `src/MyWorkHub.Core/Features/Foo/`: the domain record(s) (`FooItem`) and the service interface (`IFooService`, `CancellationToken ct = default` last). Template: `Features/Todo/{TodoItem,ITodoRepository}.cs`.
2. **Infrastructure** — `src/MyWorkHub.Infrastructure/Features/Foo/`: the `internal sealed` implementation, plus `public sealed class FooInfrastructureModule : IInfrastructureModule` registering it. Bind configuration with a `FooOptions.FromConfiguration(configuration)` singleton (see `Features/Email/EmailOptions.cs`) — or, if the Settings page will edit the section, a `LiveOptions<FooOptions>` read per use (see Configuration). For persistence: an `internal` EF entity + `IEntityTypeConfiguration<T>` in the slice (picked up by `ApplyConfigurationsFromAssembly`; repositories use `db.Set<FooEntity>()` through `IDbContextFactory<AppDbContext>`), then add a migration (`dotnet ef migrations add AddFoo --project src/MyWorkHub.Infrastructure`, design-time factory `AppDbContextFactory`). Template: `Features/Todo/{TodoRepository,TodoItemEntity,TodoItemEntityConfiguration,TodoInfrastructureModule}.cs`.
3. **Presentation** — `src/MyWorkHub.Presentation/Features/Foo/`: `FooViewModel : PageViewModel` (optional nullable services), row view models, and `public sealed class FooPresentationModule : IPresentationModule` with `MenuEntry = new("Foo", "<emoji>", typeof(FooViewModel), MENU_ORDER)` and `services.AddTransient<FooViewModel>()` (transient is enough — `NavigationService` caches one page instance for the app's lifetime). Use `MenuEntry => null` for a page reached only by navigation. Template: `Features/Todo/{TodoViewModel,TodoRowViewModel,TodoPresentationModule}.cs`.
4. **UI** — `src/MyWorkHub.UI/Features/Foo/`: `FooView.axaml(.cs)` (`x:DataType` set, compiled bindings, `DynamicResource App*` brushes only — see Theming) and `public sealed class FooViewModule : IViewModule` mapping `[typeof(FooViewModel)] = typeof(FooView)`. Template: `Features/Todo/{TodoView.axaml,TodoView.axaml.cs,TodoViewModule}.cs`.
5. **Tests** — view-model tests plus one composition test in `tests/MyWorkHub.UI.Tests/Features/Foo/` asserting the sidebar entry and view mapping exist in the default `AddUi()` composition (see `TodoDeepLinkTests.Should_contribute_a_sidebar_entry_and_a_view_in_the_default_composition`); implementation tests in `tests/MyWorkHub.Infrastructure.Tests/Features/Foo/`. Sidebar-order tests in `MainWindowViewModelTests` pin the full label list — update them when you add an entry.

Nothing else changes — no shared file, no `CompositionRoot` edit. A new automation is likewise one `IAutomationModule` + one `AutomationJob` subclass + an `AutomationCatalog` descriptor + an `Automation:<Id>` config section.

### Navigation and element-level deep links

- `NavigationTarget(Type ViewModelType, string? ElementId = null, object? Parameter = null)` (Core, so background services can build one) is the only way to navigate: `INavigationService.NavigateTo(NavigationTarget)` resolves/caches the page, makes it `CurrentPage`, and — when `ElementId` is set and the page implements `IDeepLinkTarget` (`Presentation/Navigation`) — calls `FocusElement(elementId)`. Unknown or stale ids must be ignored silently (a stale notification must never break a page).
- **Deep-link targets today**: Todo, Email, Pull requests, Work items (two kinds, prefixed `item:{id}` / `mention:{commentId}` because the id spaces overlap). Calendar, Teams, Automations and Dashboard are deliberately not targets (no notification-driven use case; automation jobs cannot reference view-model types). Each target exposes a static builder so callers never hand-format ids: `TodoViewModel.TargetFor(Guid)`, `EmailViewModel.TargetFor(string)`, `PullRequestsViewModel.TargetFor(int)`, `WorkItemsViewModel.TargetForWorkItem(int)` / `TargetForMention(int)`.
- **Opting a list into deep links** (mirror `TodoViewModel`/`TodoView`):
  1. Implement `IDeepLinkTarget`; expose `SelectedItem` and `HighlightedItem` observable properties plus a `static NavigationTarget TargetFor(...)`.
  2. `FocusElement` stores the id as pending; apply it after the list has loaded (a request on first visit arrives before the data), then set `SelectedItem = HighlightedItem = row`.
  3. In the view use a real `ListBox` (not a bare `ItemsControl`; don't wrap it in a `ScrollViewer`) with `SelectedItem="{Binding SelectedItem}"` and `behaviors:ScrollIntoViewBehavior.Item="{Binding HighlightedItem}"`. Trigger loading from the view's `OnLoaded` (not the view-model constructor) so the focus request is queued first.
  4. The shared `UI/Behaviors/ScrollIntoViewBehavior.cs` scrolls the row into view, flashes it (the `deepLinkHighlight` class styled in `Styles/AppStyles.axaml`) and writes the property back to `null`, so every request fires exactly once.
- **Notifications**: `INotificationService.Notify(title, message, severity, NavigationTarget? target = null)` (Core; implemented by `ToastNotificationService` in UI, safe from any thread). The toast and its bell-history `NotificationEntry` share one `ActivateCommand`: mark read, then `NavigateTo(Target)`. There are no bare `Action` callbacks. `NotificationCenterViewModel` is a singleton shared by the toast service (writer) and the shell (reader), capped at 200 entries; `BeginActivity`/`EndActivity` show live operations in the bell with a Stop button.
- **Developer page** (`Features/Dev`, last in the sidebar): buttons fire one test toast per `NotificationSeverity`, and one deep-link toast per target kind that points at the first *real* row loaded through that feature's service (falling back to a synthetic id — page opens, nothing highlighted — when the service is missing, fails or is empty). Use it to verify the toast → bell → navigate → scroll → highlight path by hand.

### Composition

`CompositionRoot.Build()` (App): seed `~/.MyWorkHub/{appsettings.json,review-agent.md}` from the shipped copies if missing → Serilog → `AddInfrastructure(configuration)` (EF Core factory, Data Protection + `ICredentialStore`, then every infrastructure module, so modules can consume credentials) → `AddUi()` (which calls `AddPresentation()`: navigation, `IBrowserLauncher`, notification center, shell, presentation modules; then pickers, `IThemeService`, toasts, `ViewRegistry`, `ViewLocator`, `MainWindow`) → `AddAutomations(configuration)` → build provider → migrate DB → `ShellCompositionValidator.Validate`. `App` starts Quartz once the window is up and shuts it down on exit.

**Configuration** comes from `~/.MyWorkHub/appsettings.json` (`%USERPROFILE%\.MyWorkHub\` on Windows), never from the shipped `src/MyWorkHub.App/appsettings.json`, which is only the first-run seed. Sections: `AzureAD`, `AzureDevOps`, `Email`, `Workspace`, `Automation`, `ExternalSites`, `UI`. Each slice binds its own section via `XxxOptions.FromConfiguration` (`UiOptions` lives in `Core/Configuration` because the App reads it). The file is loaded with `reloadOnChange: true` through a **polling** `PhysicalFileProvider` (not a `FileSystemWatcher`: that would be recursive over `~/.MyWorkHub/`, i.e. one inotify watch per directory of every PR-review clone under `repos/` on Linux).

- **Live (no restart)**: `AzureDevOps`, `Workspace` and `Email`. They are registered as `LiveOptions<AzureDevOpsOptions>` / `LiveOptions<WorkspaceOptions>` / `LiveOptions<EmailOptions>` (`Infrastructure/Configuration/LiveOptions.cs`), whose `Current` re-runs `FromConfiguration` on every access; consumers (`AzureDevOpsClient`, `AzureDevOpsService`, `ClaudeCliAgentRunner`, `ClaudePrReviewService`, `GitRepositoryWorkspace`, `ReviewAgentTemplateResolver`, `GraphEmailService`) read `Current` once per operation — never capture it in a constructor. `UI:Theme`/`UI:Palette` are applied live by the Settings page through `IThemeService`.
- **Read once at startup (restart after editing)**: everything else — `AzureAD`, `Automation` (Quartz schedules), `ExternalSites`, `UI:RefreshIntervalMinutes`, `UI:MinimizeToTrayOnClose`, and `UI:Theme`/`UI:Palette` as applied at launch. Move a section to `LiveOptions` only when a UI starts editing it.

**Settings page** (`Features/Settings`, sidebar just above Developer) is the one place to edit configuration — do not add ad-hoc settings UIs elsewhere; add a tab/section here. Tabs: **Appearance** (theme + palette, applied live and saved on change), **Workspace** (Claude CLI path, **work folder = where PR reviews clone**, review model, review-agent template; folder/file pickers), **Azure DevOps** (organization URL, project list, PAT status/verify/forget), **Email** (whether the Email page shows *Favorites* — hidden by default, the page then opens on the Inbox — the folders merged into it, ticked in the real mailbox tree and saved by Graph id; messages per folder) and **About** (version, data paths, repo link). Persistence is `ISettingsService` (Core) → `SettingsService` + `JsonSettingsFile` (Infrastructure): read-modify-write of only the owned keys (every other section/key preserved, case-insensitive key match), written to a temp file in the same folder, flushed, then atomically moved over the original; a malformed file is never overwritten (comments are not preserved). After each save the configuration is `Reload()`ed explicitly, so the change is visible at once. The PAT never touches the file: the Settings panel and the pages' inline prompt both go through `IAzureDevOpsConnectionService` (same credential key), and "Verify & save" saves the URL/projects first because the token is checked against that organization. Microsoft 365 sign-in stays on the page status panels and site logins on the Automations page.

**Version**: the repo-root `VERSION` file (bumped by `.github/workflows/release.yml`) is read by `Directory.Build.props` into `$(Version)` for every project, so `AssemblyInformationalVersion` is `0.1.3+<commit>` locally and in releases (a `-p:Version=` still wins); Settings → About shows it.

### Credentials

Secrets never go in `appsettings.json` — only in `ICredentialStore` (`DataProtectionCredentialStore`), and **always under a key from `Core/Abstractions/CredentialKeys.cs`** (`AZURE_DEVOPS_PAT`, `TCL_USER`/`TCL_PASSWORD`, `PEOPLENET_USER`/`PEOPLENET_PASSWORD`, `MWORK_USER`/`MWORK_PASSWORD`) — never a string literal at the call site. The values are persisted and match the pre-rewrite app; `CredentialKeysTests` pins them, because renaming one silently forgets the saved secret. Feature services wrap the store (`AzureDevOpsClient`/`AzureDevOpsConnectionService`/`GitRepositoryWorkspace` for the PAT, `IAutomationCredentialService` for site logins).

### Theming

`App.axaml` merges FluentTheme + `UI/Styles/AppStyles.axaml` and statically includes the GitHub palette so tokens always resolve. `PaletteManager` swaps the palette dictionary (`UI/Themes/{GitHub,VSCode,OneDark,TokyoNight}.axaml`, each defining the same `App*` keys for Light and Dark) and `UI.Theme` (`System`/`Light`/`Dark`) sets the theme variant at startup; `IThemeService` (`AvaloniaThemeService`) is the runtime seam for both. Views must use `{DynamicResource App*Brush}` (`AppTextBrush`, `AppTextMutedBrush`, `AppSurfaceBrush`, `AppAccentBrush`, `AppDangerBrush`, …) — never hardcoded colours — and no OS-specific icon fonts (use emoji or a `PathIcon`, as the notification bell does).

## Integration specifics that bite

- **Azure DevOps uses raw `HttpClient` REST, NOT the TFS/VSS client SDK** (vulnerable SqlClient transitive dependency and bloat — `docs/adr/0002`). `AzureDevOpsClient` is the thin transport: PAT Basic auth, 401/203 → `AzureDevOpsNotConnectedException`.
- **AzDO comment @mentions use a preview-only API** — `_apis/wit/workItems/{id}/comments` has been `7.1-preview.3` since API 5.1 and was never promoted to GA. It is called best-effort: each work item's comments are fetched in their own `try/catch` in `AzureDevOpsService`, and `WorkItemsViewModel` wraps the whole mentions step, so a failure only hides the Mentions section. If it ever goes GA, change `COMMENTS_API_VERSION` in `AzureDevOpsService.cs` (one constant). Rationale: `docs/adr/0001-azdo-work-item-comments-preview-api.md`. `WorkItemMentionScanner` strips the comment HTML and looks for `@{displayName}` (case-insensitive). Read state lives in the `SeenMentions` table (PK `CommentId`, value never generated) via `ISeenMentionRepository`.
- **Microsoft Graph 6.x** (6.7.0, Kiota-based). Tests use the `GraphServiceClient(IRequestAdapter)` seam with `FakeGraphRequestAdapter`. Graph services throw `GraphNotConnectedException` when not signed in; pages show the `GraphPageStatus` sign-in panel.
- **MSAL**: always `WithUseEmbeddedWebView(false)` (system browser, required by Cegid SSO). Token cache at `~/.MyWorkHub/msal_token_cache.bin`, encrypted per OS by `MsalCacheHelper` (DPAPI / Keychain / libsecret).
- **SQLite**: `Microsoft.EntityFrameworkCore.Sqlite.Core` + `SQLitePCLRaw.bundle_e_sqlite3` (cross-platform native bundle). The bundled-`e_sqlite3` advisory `GHSA-2m69-gcr7-jv3q` is suppressed in `Directory.Build.props` with rationale (single-user local DB, never from an untrusted source).
- **EF Core** migrations are applied on startup by `CompositionRoot` (`Database.Migrate()`, before validation) — never require a manual `dotnet ef database update`.
- **Credential store is cross-platform**: `Microsoft.AspNetCore.DataProtection` (DPAPI on Windows, AES-256 + HMAC key ring elsewhere) — not the Windows-only `ProtectedData`. Key ring at `~/.MyWorkHub/keys/`, registered with `AddDataProtection().PersistKeysToFileSystem(...).SetApplicationName("MyWorkHub")` before any feature module.
- **Playwright**: Chromium only, behind `IBrowserService`/`IBrowserSession` (Infrastructure). `DownloadFileAsync` starts waiting for the download **before** clicking the trigger, writes to disk and returns a path (never `byte[]`).
- **`RemoteWorkSyncJob`**: the three sync steps (Outlook, PeopleNet, mwork) each run as their own isolated step with their own `try/catch` (`AutomationStepRecorder`) — one failure must never abort the others. This is why `CA1031` (broad catch) is disabled.
- **`IBrowserLauncher` ≠ `IBrowserService`**: `IBrowserLauncher` (Core interface, `Presentation/Platform/BrowserLauncher`, registered by `AddPresentation()`) opens a URL in the user's default browser via shell-execute. `IBrowserService` is the Playwright automation wrapper. Do not confuse them.
- **Single instance**: `SingleInstanceGuard` uses a named pipe (`MyWorkHub-single-instance`; a Unix socket under the temp dir on Linux/macOS). A second launch just asks the running instance to show its window and exits — to launch an isolated copy for testing, point both `HOME` and `TMPDIR` (short path — Unix socket paths are limited to 108 chars) at scratch directories.
- Runtime data lives under `~/.MyWorkHub/` (`AppPaths`): SQLite db, appsettings, logs, temp downloads, error screenshots, key ring, MSAL cache, review-agent template, PR review clones (`repos/`).

## AI features (Claude CLI)

Both AI features go through **one abstraction**: `ICliAgentRunner.RunAsync(CliAgentRequest, IProgress<string>?, ct)` (Core `Features/CliAgent`), implemented once by `ClaudeCliAgentRunner` (Infrastructure) — the **only** code that builds a `claude` command line. It runs `claude -p --output-format text` with the binary from `Workspace:ClaudeExecutablePath` (default: `claude` on `PATH`) and throws `CliAgentException` with a user-facing message on failure. Security invariants — keep them in the runner, never re-implement them in a feature:

- **System prompt via the `--system-prompt` argument, user message via stdin** — never the other way round, so (possibly attacker-controlled) content cannot replace the instructions.
- **Never pass `--bare`** — it disables the OAuth/keychain login the user already has.
- **Isolation**: with `WorkingDirectory = null` the run happens in a fresh empty temp directory (`Directory.CreateTempSubdirectory`), deleted afterwards, so no `CLAUDE.md` or repository files leak into the context. `StrictMcpConfig` (default `true`) adds `--strict-mcp-config`, so no ambient MCP servers load.
- **Tool restrictions** per request: `DisallowedTools` → `--disallowedTools`, `AllowedTools` → `--allowedTools` (one argv entry per rule; headless runs cannot answer permission prompts).

Feature-specific rules:
- **Email digest** (`ClaudeEmailSummaryService`, `EmailSummaryPrompt`): runs in the isolated temp dir with every dangerous tool denied (`Bash, Edit, Write, NotebookEdit, WebFetch, WebSearch, Task, Agent`) and strict MCP. Email content is untrusted: it is quarantined in a **nonce-delimited block** (`<<<EMAILS-{nonce}>>>` … `<<<END-EMAILS-{nonce}>>>`, 16 fresh random bytes per call, regenerated if the content happens to contain it), so an email cannot forge the closing marker; the system prompt tells the model to treat the block as data only. Bodies are fetched lazily (`IEmailService.GetEmailBodyAsync`) and capped per message and per digest.
- **PR review** (`ClaudePrReviewService`, `GitRepositoryWorkspace`, `ReviewAgentTemplateResolver`): clones/fetches the repository into `Workspace:WorkFolderPath` (default `~/.MyWorkHub/repos/`), checks out the source branch, then runs the CLI **in the clone** with read-only tools pre-approved (`Read, Grep, Glob`, `Bash(git diff|log|show|status|blame:*)`) and editing/web/sub-agent tools denied. **The AzDO PAT reaches git only through environment variables** (`GIT_CONFIG_COUNT`/`GIT_CONFIG_KEY_0=http.extraHeader`/`GIT_CONFIG_VALUE_0=Authorization: Basic …`, plus `GIT_TERMINAL_PROMPT=0`) — never on the command line, in the remote URL or in the clone's `.git/config`. The system prompt is the review-agent Markdown template, resolved in order: per-review override (chosen for one PR from the Pull requests page) → `Workspace:ReviewAgentPath` → the seeded `~/.MyWorkHub/review-agent.md` (shipped as `src/MyWorkHub.App/review-agent.md`) → the compiled-in `DEFAULT_AGENT_TEMPLATE`; a user-supplied path that cannot be read is skipped with a progress warning. The model is `Workspace:ReviewModelId`.

## Library/API docs

Use the `find-docs` skill / `ctx7` CLI (Context7) for current docs on Avalonia, Microsoft Graph, MSAL, EF Core, Quartz, Playwright and CommunityToolkit.Mvvm before relying on memory — these APIs move fast.
