# MyWorkHub — Architecture

**Status:** Current — describes the code as of 2026-11 (after the feature-module rewrite).  
**Scope:** the big picture for a human reader. Exact signatures live in the code (`src/MyWorkHub.Core/Features/<Name>/` for the interfaces); the working rules for contributors and coding agents live in [`AGENTS.md`](../AGENTS.md); the reasons behind the key choices live in [`adr/`](adr/README.md). When this document and the code disagree, the code wins — fix this document.

---

## 1. What the app is

A single-user desktop app (**Avalonia UI 12 + .NET 10**; Windows first, also Linux and macOS) that gathers one engineer's daily workflow into one window: Outlook mail and calendar, Teams chats, Azure DevOps pull requests and work items, a local todo list, scheduled browser automations, and two AI features that drive the locally installed Claude CLI (email digest, PR code review). Everything runs locally; there is no server.

---

## 2. Solution layout

```
MyWorkHub/
├── src/
│   ├── MyWorkHub.Core/            # Domain records, service interfaces, module contracts, navigation — pure C#
│   ├── MyWorkHub.Infrastructure/  # Graph, MSAL, AzDO REST, EF Core + SQLite, credential store, Claude CLI, Playwright
│   ├── MyWorkHub.Presentation/    # View models (CommunityToolkit.Mvvm), navigation, notification center — no Avalonia
│   ├── MyWorkHub.UI/              # Avalonia views, view modules, ViewLocator, behaviors, themes, toasts, pickers
│   ├── MyWorkHub.Automation/      # Quartz.NET jobs and their scheduling
│   └── MyWorkHub.App/             # Entry point, CompositionRoot, tray, single-instance guard
├── tests/
│   ├── MyWorkHub.Core.Tests/
│   ├── MyWorkHub.Infrastructure.Tests/
│   ├── MyWorkHub.UI.Tests/        # view models + headless Avalonia tests of real views
│   └── MyWorkHub.Automation.Tests/
├── docs/                          # this file, prd.md, tasks.md (historical), adr/
├── AGENTS.md
└── MyWorkHub.slnx
```

Inside every layer, code is grouped **by feature**: `Features/<Name>/` (Email, Calendar, Teams, PullRequests, WorkItems, Todo, Automations, Dashboard, Settings, Dev, …) plus shared non-page slices (`GraphAuth`, `AzureDevOps`, `CliAgent`, `EmailSummary`, `PrReview`, `Workspace`, `Updates`).

---

## 3. Layers and dependency rule

```
                ┌──────────────────────────────┐
                │        MyWorkHub.App         │  composition root — the only project
                │ (entry point, tray, Quartz)  │  that references everything
                └──┬──────────┬─────────┬──────┘
                   │          │         │
        ┌──────────▼───┐ ┌────▼─────┐ ┌─▼──────────────┐
        │      UI      │ │  Infra-  │ │   Automation   │
        │  (Avalonia)  │ │ structure│ │  (Quartz jobs) │
        └──────┬───────┘ └────┬─────┘ └───────┬────────┘
               │              │               │
        ┌──────▼───────┐      │               │
        │ Presentation │      │               │
        │ (view models)│      │               │
        └──────┬───────┘      │               │
               │              │               │
        ┌──────▼──────────────▼───────────────▼────────┐
        │                 MyWorkHub.Core               │
        │  records · interfaces · module contracts     │
        └──────────────────────────────────────────────┘
```

- **UI and Presentation never touch Infrastructure.** They depend on Core interfaces only; the App wires the implementations in.
- **Presentation has no Avalonia reference**, so view models are tested as plain C#.
- **Core and Presentation are `IsAotCompatible`**: the trim/AOT analyzers act as a tripwire against reflection there. The app itself is not AOT-published (EF Core, Graph, MSAL, Playwright and Quartz use reflection).

---

## 4. Feature modules (how a feature plugs in)

There is no central registration file, no hardcoded sidebar and no naming-convention view lookup. Each feature contributes one **module class per layer**, discovered by reflection at startup ([ADR-0003](adr/0003-feature-module-architecture.md)):

| Contract | Lives in | Contributes |
|---|---|---|
| `IInfrastructureModule` | `Infrastructure/Features/<Name>/` | service implementations + options |
| `IPresentationModule` | `Presentation/Features/<Name>/` | view-model registrations + an optional sidebar entry (`NavigationItem`, sorted by `Order`) |
| `IViewModule` | `UI/Features/<Name>/` | the view-model → view map used by `ViewLocator` |
| `IAutomationModule` | `Automation/Scheduling/` | an automation descriptor + its Quartz job type |

`ModuleDiscovery.Find<T>()` instantiates every non-abstract, non-generic `T` with a public parameterless constructor in the scanned assemblies (module classes are `public sealed` by convention). Adding a feature means adding these folders and nothing else; the Todo slice is the reference template (step-by-step recipe in `AGENTS.md`).

### Composition and fail-loud startup

`CompositionRoot.Build()`:

1. seeds `~/.MyWorkHub/appsettings.json` and `review-agent.md` from the shipped copies if they are missing;
2. configures Serilog;
3. `AddInfrastructure()` — EF Core factory, Data Protection + credential store, then every infrastructure module;
4. `AddUi()` — navigation, notification center, shell, presentation and view modules, themes, toasts, `ViewRegistry`, `ViewLocator`;
5. `AddAutomations()` — Quartz jobs from the automation modules;
6. builds the container, **applies EF Core migrations**, then runs `ShellCompositionValidator`: every page view model and sidebar target must resolve and have a constructible view, otherwise launch aborts with the full list of problems.

Consequence: page view-model constructors run at startup, so they stay cheap.

---

## 5. Pages, navigation and notifications

- **Pages** are `PageViewModel`s. Every service is an optional nullable constructor parameter; a missing or unconfigured service degrades the page (a notice, disabled buttons) instead of failing.
- **Navigation** goes through `INavigationService.NavigateTo(NavigationTarget)`. A target can carry an `ElementId`; pages implementing `IDeepLinkTarget` (Todo, Email, Pull requests, Work items) select, scroll to and flash that row ([ADR-0004](adr/0004-element-level-deep-linking.md)). Unknown ids are ignored.
- **Notifications**: `INotificationService.Notify(…, NavigationTarget?)` shows a toast and records it in the bell history; clicking either navigates to the target.
- **Periodic refresh**: `UpdateWatcher` runs from launch to exit (also while in the tray). Every `UI:RefreshIntervalMinutes` it checks each enabled source (mail, chats, PRs, work items, mentions, calendar), raises one deep-linked notification per new item, then refreshes the page on screen. The first check of a source records a baseline only.
- **Commands and cancellation**: a page command absorbs the cancellation of a superseded run and reports every other failure, including cancellations a library raised on its own ([ADR-0008](adr/0008-commands-never-rethrow-cancellation.md)). Per-row commands that run concurrently (tick a todo, run an automation, open a mention) take no cancellation token, because a click on another row would cancel theirs.

---

## 6. Integrations

| Integration | How |
|---|---|
| Microsoft Graph (mail, calendar, Teams) | `Microsoft.Graph` 6.x (Kiota). Tests use the `GraphServiceClient(IRequestAdapter)` seam. Not signed in → `GraphNotConnectedException` → the page's sign-in panel. |
| Sign-in | MSAL with the **system browser** (`WithUseEmbeddedWebView(false)`, required by the corporate SSO). Token cache `~/.MyWorkHub/msal_token_cache.bin`, encrypted per OS (DPAPI / Keychain / libsecret). Only user-consentable scopes are requested. |
| Azure DevOps | Raw `HttpClient` REST with PAT Basic auth, **not** the TFS/VSS SDK ([ADR-0002](adr/0002-azdo-rest-not-sdk.md)). Comment @mentions use a preview-only API, called best-effort ([ADR-0001](adr/0001-azdo-work-item-comments-preview-api.md)). |
| Storage | EF Core + SQLite (`Sqlite.Core` + `bundle_e_sqlite3`, cross-platform). Entities and configurations live in their feature slice; migrations are applied automatically at startup. |
| Secrets | `ICredentialStore` on ASP.NET Core Data Protection (DPAPI on Windows, AES + HMAC key ring elsewhere; keys in `~/.MyWorkHub/keys/`). Always addressed through `CredentialKeys` constants. Never in `appsettings.json`. |
| Browser automation | Playwright, Chromium only, behind `IBrowserService` / `IBrowserSession`. Downloads are awaited before the click and written to disk. |
| Scheduling | Quartz.NET embedded in the process, started once the window is up and shut down on exit. |
| Default browser | `IBrowserLauncher` (shell-execute) — distinct from the Playwright `IBrowserService`. |

### Email reading pane

The Email page is a mail client (folder tree, optional *Favorites* merging chosen folders, HTML reading pane). Message HTML is untrusted: it is rendered in a native WebView behind a strict Content-Security-Policy, remote images load only on request, and every navigation is filtered by `ReaderNavigationGuard` ([ADR-0007](adr/0007-email-html-in-sandboxed-native-webview.md)).

### Automations

Two automations, each an `AutomationJob` subclass with a descriptor in `AutomationCatalog` and an `Automation:<Id>` config section:

- **TransportReimbursement** — downloads the TCL attestation with Playwright and emails it to the configured billing address through Graph.
- **RemoteWorkSync** — pushes the month's remote-work plan (edited on the Automations page) to Outlook, PeopleNet and mwork. The three steps are **isolated**: each has its own `try/catch` (`AutomationStepRecorder`), so one failure never stops the others.

Every run and its per-step outcomes are logged to SQLite and shown on the Automations page; a finished run raises a notification. There is no automatic retry — a failed run is rerun with "Run now".

---

## 7. AI features (Claude CLI)

Both features go through one abstraction, `ICliAgentRunner`, implemented only by `ClaudeCliAgentRunner` — the single place that builds a `claude` command line ([ADR-0005](adr/0005-cli-agent-runner-is-claude-specific.md)). Invariants:

- system prompt passed as an argument, user content through stdin;
- by default the run happens in a fresh empty temp directory with `--strict-mcp-config`, so no repository files or ambient MCP servers leak in;
- tool allow/deny lists per request (headless runs cannot answer permission prompts).

| Feature | Specifics |
|---|---|
| Email digest | All dangerous tools denied. Email content is wrapped in a **nonce-delimited block** the model is told to treat as data, so an email cannot forge the end marker. |
| PR review | Clones/fetches into `Workspace:WorkFolderPath` and runs **in the clone** with read-only tools only. The AzDO PAT reaches git through environment variables only — never the command line, remote URL or `.git/config`. The system prompt is a review-agent Markdown template (per-review override → configured path → seeded file → built-in default). |

---

## 8. Configuration

The runtime file is `~/.MyWorkHub/appsettings.json` (the shipped `src/MyWorkHub.App/appsettings.json` is only the first-run seed). Sections: `AzureAD`, `AzureDevOps`, `Email`, `Workspace`, `Automation`, `ExternalSites`, `UI`, `Notifications`. Each slice binds its own section through `XxxOptions.FromConfiguration`.

- **Applied live**: `AzureDevOps`, `Workspace`, `Email` (through `LiveOptions<T>`, read once per operation), theme and palette, refresh interval and notification switches ([ADR-0006](adr/0006-scoped-live-settings-reload.md)).
- **Read at startup**: everything else (`AzureAD`, `Automation` schedules, `ExternalSites`, `UI:MinimizeToTrayOnClose`).

The **Settings page** is the only editor: it rewrites only the keys it owns, atomically (temp file + move), never overwrites a malformed file, then reloads the configuration.

---

## 9. Theming

Fluent theme plus app styles, with four swappable palettes (GitHub, VSCode, OneDark, TokyoNight), each defining the same `App*` brush keys for light and dark. Views use `{DynamicResource App*Brush}` only — no hardcoded colours, no OS-specific icon fonts.

---

## 10. Quality gates

- `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisMode=All`: any warning fails the build; each relaxed rule is justified in `.editorconfig`.
- xUnit v3, one test project per layer mirroring `Features/<Name>/`; headless Avalonia tests click real views; the real `AddUi()` composition is validated in tests.
- Significant decisions are recorded as ADRs.

---

## 11. Runtime data

```
~/.MyWorkHub/                (%USERPROFILE%\.MyWorkHub\ on Windows)
├── MyWorkHub.db             SQLite database
├── appsettings.json         user configuration
├── review-agent.md          default PR review-agent template
├── msal_token_cache.bin     encrypted MSAL token cache
├── keys/                    Data Protection key ring
├── logs/                    Serilog rolling files
├── temp/                    downloads (e.g. the transport attestation)
├── errors/                  Playwright failure screenshots
└── repos/                   PR review clones
```
