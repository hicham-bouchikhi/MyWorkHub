# MyWorkHub — Task Matrix

**Version:** 1.0  
**Date:** 2026-06-22  
**Total MVP tasks:** 78 | **V2 tasks:** 6

Effort scale: XS < 1h · S = 1–3h · M = 3–6h · L = 1 day · XL = 2+ days

---

## Phase 0 — Foundation

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T001 | Create .NET 10 solution with 5 projects (App, UI, Core, Infrastructure, Automation) | P0 | S | — | DONE |
| T002 | Configure Microsoft.Extensions.DependencyInjection composition root in MyWorkHub.App | P0 | S | T001 | DONE |
| T003 | Bootstrap Avalonia UI 12 with CommunityToolkit.Mvvm and Fluent theme in MyWorkHub.App | P0 | M | T001 | DONE (Fluent theme + CommunityToolkit.Mvvm; ReactiveUI dropped — too niche for reliable AI authoring) |
| T004 | Add appsettings.json loading (Microsoft.Extensions.Configuration) with typed options classes | P0 | S | T002 | DONE |
| T005 | Implement ICredentialStore with Windows DPAPI (ProtectedData.Protect/Unprotect) | P0 | S | T002 | DONE |
| T006 | Set up EF Core + SQLite AppDbContext with entities: TodoItem, AutomationRun, AppCredential, RemoteWorkSchedule | P0 | M | T002 | DONE |
| T007 | Create initial EF Core migration and auto-apply on startup | P0 | S | T006 | DONE |
| T008 | Add Serilog rolling file logger wired to Microsoft.Extensions.Logging | P0 | S | T002 | DONE |
| T009 | Create runtime folder structure (~/.MyWorkHub/) on first launch | P0 | XS | T002 | DONE |

---

## Phase 1 — Microsoft Graph Integration

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T010 | **[MANUAL — Azure Portal, no code]** Register Azure AD app in personal Azure subscription: set as multi-tenant, add delegated permissions Mail.Read, Calendars.ReadWrite, Chat.Read, ChannelMessage.Read.All, User.Read, offline_access. Copy the generated Client ID into appsettings.json AzureAD.ClientId | P0 | S | — | DONE (ClientId + TenantId set in shipped & runtime appsettings.json; configured single-tenant 604e5547-… rather than the documented "common") |
| T011 | Implement MSAL.NET interactive auth flow (browser popup on first run, disk-cached token) | P0 | M | T010, T005 | DONE (MsalPublicClientFactory + MsalTokenProvider, system browser via WithUseEmbeddedWebView(false), DPAPI token cache at msal_token_cache.bin) |
| T012 | Define IEmailService interface in MyWorkHub.Core | P0 | XS | T001 | DONE |
| T013 | Implement GraphEmailService: GetImportantEmailsAsync (unread + flagged), GetUnreadCountAsync | P0 | M | T011, T012 | DONE (GraphEmailService + GraphEmailMapper/Query; SendEmailAsync deferred to T055) |
| T014 | Define ICalendarService interface in MyWorkHub.Core | P0 | XS | T001 | DONE |
| T015 | Implement GraphCalendarService: CreateRemoteWorkEventsAsync (Working Elsewhere, Europe/Paris, all-day) | P0 | M | T011, T014 | DONE (RemoteWorkEventFactory: all-day, WorkingElsewhere, Europe/Paris) |
| T016 | Implement GraphCalendarService: GetExistingRemoteWorkDatesAsync (to detect already-synced days) | P1 | S | T015 | DONE (RemoteWorkCalendarQuery: month window + remote-work event filter) |

---

## Phase 2 — Azure DevOps Integration

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T017 | Define IAzureDevOpsService interface in MyWorkHub.Core | P0 | XS | T001 | DONE |
| T018 | Implement AzureDevOpsService: authenticate with PAT from ICredentialStore | P0 | S | T005, T017 | DONE (REST + HttpClient, NOT the SDK — see note below; PAT Basic auth from ICredentialStore key AZDO_PAT) |
| T019 | Implement GetPullRequestsForReviewAsync: query PRs where reviewer = current user across configured projects | P0 | M | T018 | DONE (resolves current user via connectionData, queries active PRs per project by reviewerId) |
| T020 | Implement GetMyWorkItemsAsync: WIQL query for current sprint items assigned to current user | P0 | M | T018 | DONE (WIQL @Me + @CurrentIteration, then work-items batch fetch + field mapping) |

> **Phase 1/2 implementation notes (2026-06-22):**
> - **AzDO uses REST over HttpClient, not the SDK** (deviation from ARCHITECTURE.md §5). The `Microsoft.TeamFoundationServer.Client`/`Microsoft.VisualStudio.Services.Client` SDK pulled `Microsoft.Data.SqlClient` + a vulnerable `System.Security.Cryptography.Xml 5.0.0` (GHSA-vh55-786g-wjwj), breaking the warnings-as-errors build. REST is also far more unit-testable.
> - **Graph SDK is 6.2.0** (doc said "5.x"); still Kiota-based, same `GraphServiceClient(IRequestAdapter)` seam.
> - **JSON uses a source-generated `JsonSerializerContext`** (`AzureDevOpsJson`) — reflection-based `JsonSerializer` would trip the IL2026/IL3050 tripwire (errors in `.editorconfig`).
> - **SQLite:** switched to `Microsoft.EntityFrameworkCore.Sqlite.Core` + `SQLitePCLRaw.bundle_winsqlite3` (Windows-native SQLite, avoids the bundled `e_sqlite3` flagged by GHSA-2m69).
> - **T010 done:** `AzureAD.ClientId` + `TenantId` set in both appsettings.json copies. Configured **single-tenant** (`604e5547-…`) rather than the documented `"common"` — if `@cegid.com` sign-in fails with "account doesn't exist in this tenant", switch `TenantId` back to `"common"`.
> - **Sign-in "Need admin approval" fixed (2026-06-22):** `ChannelMessage.Read.All` was being requested at sign-in; it needs admin consent and (bundled into MSAL's single interactive call) blocked the whole sign-in. Removed from `GraphScopes.Delegated` — sign-in now requests only user-consentable scopes (Mail.Read, Calendars.ReadWrite, Chat.Read, User.Read). Teams channel @mentions degrade gracefully (T076). If the wall still appears after this, the tenant has user consent disabled for all apps → needs a one-time admin grant.

---

## Phase 3 — UI Shell & Navigation

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T021 | Design main window layout: collapsible sidebar navigation + content area + top status bar | P0 | M | T003 | DONE (MainWindow.axaml: top status bar with hamburger toggle + current-section label, sidebar Border bound to SidebarWidth, ContentControl bound to Navigation.CurrentPage via ViewLocator) |
| T022 | Implement NavigationService (manual page switching — hand-written, no ReactiveUI routing) | P0 | S | T021 | DONE (UI/Navigation: INavigationService + NavigationService resolves page VMs from DI, CurrentPage as ObservableProperty; ReactiveUI stays dropped — confirmed with Hicham, CommunityToolkit.Mvvm kept) |
| T023 | Create sidebar nav items: Dashboard, Email, Pull Requests, Work Items, Teams, Todo, Remote Work, Automations, Settings | P0 | S | T021 | DONE (9 items incl. Teams, exact order; each maps to a placeholder PageViewModel/View pair. Note: Teams added vs. original 8-item list) |
| T024 | Implement system tray icon with "Open MyWorkHub" and "Quit" menu items | P1 | S | T003 | DONE (TrayIcon in App.axaml using /Assets/avalonia-logo.ico; NativeMenu Open restores+activates window, Quit shuts down) |
| T025 | Implement "minimize to tray on close" behavior (configurable) | P1 | S | T024 | DONE (App.axaml.cs intercepts Window.Closing, cancels + hides when UI.MinimizeToTrayOnClose is true; Quit bypasses via _isExiting flag) |
| T026 | Implement app-wide toast notification service for errors and automation results | P1 | S | T021 | DONE (INotificationService in Core — toolkit-agnostic; ToastNotificationService in UI wraps WindowNotificationManager, marshals via Dispatcher so background/automation callers are safe; injected as singleton) |

---

## Phase 4 — Dashboard

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T027 | Dashboard ViewModel: aggregate counts from IEmailService, ITeamsService, IAzureDevOpsService, ITodoRepository. ITeamsService is optional — if it throws or returns empty, show 0 for Teams count without failing the dashboard | P0 | M | T013, T075, T019, T020 | DONE (DashboardViewModel: every source injected as optional/nullable and wrapped in its own try/catch; Teams failure logs + shows 0, never trips offline; missing services → 0/empty. No infra calls — only Core interfaces) |
| T028 | Dashboard View: email widget (count + top 3 preview), Teams widget (unread count), PR widget (count), work items widget (overdue count) | P0 | M | T027 | DONE (clickable metric cards + wide "Important email" card with top-3 ItemsControl; each card Command navigates to its page via INavigationService) |
| T029 | Dashboard View: todo widget (due today), automation widget (next run + last status) | P0 | S | T027 | DONE (todo due-today/overdue count card; automation card shows last-run status from IAutomationLogger when wired — next-run stays "Not scheduled" until Quartz lands in Phase 9) |
| T030 | Auto-refresh timer (every 5 min, configurable) wired to all dashboard data sources | P1 | S | T027 | DONE (DispatcherTimer at UI.RefreshIntervalMinutes; started/stopped by DashboardView OnLoaded/OnUnloaded so it only ticks while on screen — no leak from the transient VM) |
| T031 | Offline indicator: detect no network, show "last updated" timestamp on stale data | P2 | S | T027 | DONE (IsOffline = core-source failure OR NetworkInterface.GetIsNetworkAvailable() false; StatusText shows "Last updated HH:mm:ss" / "Offline — last update HH:mm"; banner shown when offline) |

> **Phase 4 implementation notes (2026-06-22):**
> - **All dashboard data sources are injected as optional (nullable, `= null`).** The default DI container honours default parameter values, so the dashboard constructs even when a source isn't registered yet (Teams/Todo/Automation) and the existing `AddUi()`-only test harness still resolves the full window graph. In production, `IEmailService`/`IAzureDevOpsService` are wired by `AddInfrastructure`; the rest light up when their phases register implementations.
> - **Interfaces pre-created for the dashboard:** `ITeamsService` + `TeamsChatItem` (this *is* T074 — done), plus `ITodoRepository` and `IAutomationLogger` (interfaces only; their EF Core implementations remain **T045** and **T049**). All three are verbatim from ARCHITECTURE.md §6.
> - **Logging convention established:** UI/VM logging uses the `[LoggerMessage]` source generator (CA1848 is enforced as error under `AnalysisMode=All`). First logging in the repo — follow this pattern.
> - **Timer lifecycle is view-driven:** `DashboardView.OnLoaded/OnUnloaded` start/stop the `DispatcherTimer`, so the transient VM isn't kept alive by a detached timer after navigating away.
> - **Automation widget is partial:** last-run status reads from `IAutomationLogger` (job `"TransportReimbursementJob"`) once it exists; "next run" needs the Quartz scheduler (Phase 9) and shows "Not scheduled" until then.
> - **Widget navigation** uses `INavigationService.NavigateTo` directly; the sidebar's `SelectedItem` highlight does not re-sync (known limitation of the Phase 3 navigation design — not introduced here).
> - **Update (2026-06-22, this build):** the deferred optional widgets are now wired — `ITeamsService → GraphTeamsService` (T075) and `ITodoRepository → TodoRepository` (T045) are registered in `AddInfrastructure`, so the Teams unread count and Todo due-today widgets light up with no dashboard code change. The automation widget still shows defaults until Phase 9 (`IAutomationLogger`). No new Phase 4 work was required — T027–T031 were already DONE.

---

## Phase 5 — Email Digest

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T032 | Email list ViewModel: load from GraphEmailService, expose filtered observable collection | P0 | M | T013 | DONE (EmailViewModel + EmailRow; client-side sender/subject filter via OnSearchTextChanged) |
| T033 | Email list View: sender, subject, preview, received date, flagged indicator | P0 | M | T032 | DONE (EmailView.axaml; loading/empty/error states, OnLoaded refresh) |
| T034 | Filter bar: search by sender or subject keyword | P1 | S | T033 | DONE (SearchText TextBox two-way bound; case-insensitive Contains filter) |
| T035 | "Open in Outlook Web" action per email item (open browser to outlook.office.com with message ID) | P1 | S | T033 | DONE (OpenCommand → IBrowserLauncher https://outlook.office.com/mail/id/{id}) |

---

## Phase 5b — Teams Message Digest

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T074 | Define ITeamsService interface in MyWorkHub.Core (GetUnreadChatsAsync, GetUnreadCountAsync) | P0 | XS | T001 | DONE (defined alongside Phase 4 + TeamsChatItem model; GraphTeamsService impl remains T075) |
| T075 | Implement GraphTeamsService: call GET /me/chats, filter to chats where unreadMessageCount > 0, return as TeamsChatItem list | P0 | M | T011, T074 | DONE (GraphTeamsService + GraphTeamsMapper; see deviation note below — Graph `chat` has **no** `unreadMessageCount`, so "unread" is derived from `viewpoint.lastMessageReadDateTime` vs `lastMessagePreview.createdDateTime`. `$expand=lastMessagePreview`, `$orderby=lastMessagePreview/createdDateTime desc`, `$top=50`) |
| T076 | Graceful degradation: if ChannelMessage.Read.All consent is missing, log warning and return only DMs/group chats — do not throw | P1 | S | T075 | DONE (any Graph failure — incl. a missing-consent 403 — is logged via `[LoggerMessage]` and swallowed; both methods return empty/0, never throw. `/me/chats` only ever returns DMs/group chats, so channel @mentions are simply never included — the expected degraded mode) |
| T077 | Teams list ViewModel: unread conversations, sender, preview, unread count per chat | P0 | M | T075 | DONE (TeamsViewModel + TeamsChatRow; UnreadCountText "N unread") |
| T078 | Teams list View: conversation list, unread badge, "Open in Teams" deep link action | P0 | M | T077 | DONE (TeamsView.axaml; unread badge shown when HasUnread, OpenCommand → DeepLinkUrl) |

> **Phase 5b implementation notes (2026-06-22, T075/T076 — service layer only; T077/T078 page UI still TODO):**
> - **DEVIATION — `unreadMessageCount` does not exist on the Graph `chat` resource.** ARCHITECTURE.md §6/§12 and the task text say to "filter to chats where `unreadMessageCount > 0`" and "sum `unreadMessageCount` across `/me/chats`". Verified against Microsoft Learn (chat resource type, List chats): the `chat` resource exposes `viewpoint` (`chatViewpoint`: `isHidden`, `lastMessageReadDateTime`) and the `lastMessagePreview` relationship (`chatMessageInfo`), but **no `unreadMessageCount` field anywhere**. "Unread" is therefore derived in `GraphTeamsMapper.IsUnread`: a chat is unread when it has a `lastMessagePreview.createdDateTime` that is newer than `viewpoint.lastMessageReadDateTime` (or the chat was never read). Graph also has no per-chat message tally, so `TeamsChatItem.UnreadCount` is reported as **1 = one unread conversation**.
> - **`GetUnreadCountAsync` counts unread *conversations*, not messages.** It still honours §12's intent (a dedicated lightweight call — only `$expand=lastMessagePreview`, no orderby, no read-model projection; it does **not** call `GetUnreadChatsAsync` and count results).
> - **Mapper is the unit-tested seam** (7 tests), mirroring `GraphEmailMapper`; the thin SDK-calling `GraphTeamsService` follows the same untested-by-design pattern as `GraphEmailService`/`GraphCalendarService`. **Not yet exercised against live Graph** — needs a signed-in session (see report).
> - **DI:** `ITeamsService → GraphTeamsService` registered in `AddInfrastructure`. The dashboard's optional `ITeamsService` now lights up automatically (Teams unread widget).

---

## Phase 6 — PR Review Queue

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T036 | PR list ViewModel: load from AzureDevOpsService, sort by age, expose age-in-days | P0 | M | T019 | DONE (PullRequestsViewModel + PullRequestRow; loads via IAzureDevOpsService, OrderByDescending(AgeDays) = oldest first; AgeDays/AgeText exposed) |
| T037 | PR list View: title, author, repo, age, vote status badge (colour-coded) | P0 | M | T036 | DONE (PullRequestsView; VoteStatusToBrushConverter colours the badge green/amber/red/grey) |
| T038 | Highlight PRs older than 2 days in amber, older than 5 days in red | P1 | S | T037 | DONE (PullRequestRow.IsAging >2d, IsStale >5d → Classes.aging/.stale on the row border) |
| T039 | "Open in AzDO" action per PR (open browser to PR URL) | P0 | XS | T037 | DONE (OpenCommand → IBrowserLauncher.Open(row.Url), UI/Platform/BrowserLauncher via shell-execute) |
| T040 | Taskbar badge count reflecting pending PR reviews (Windows taskbar overlay icon) | P2 | S | T036 | TODO |

---

## Phase 7 — Work Items

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T041 | Work items ViewModel: load from AzureDevOpsService, group by state (Active / New / Resolved) | P0 | M | T020 | DONE (WorkItemsViewModel groups by state, canonical order Active→New→Resolved then others; WorkItemGroup/WorkItemRow) |
| T042 | Work items View: title, type icon, state chip, priority, effort estimate | P0 | M | T041 | DONE (WorkItemsView; type emoji icon, WorkItemStateToBrushConverter state chip, priority + effort. NOTE: extended Core WorkItem model with `Effort` (StoryPoints→Effort field) since §7 lacked it) |
| T043 | Overdue highlighting (past sprint end or past due date) | P1 | S | T042 | DONE (WorkItemRow.IsOverdue = DueDate past & state not done → Classes.overdue red border. Sprint-end not available without iteration data; due-date used) |
| T044 | "Open in AzDO" action per work item | P0 | XS | T042 | DONE (OpenCommand → IBrowserLauncher.Open(row.Url)) |

---

## Phase 8 — Personal Todo

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T045 | ITodoRepository implementation using EF Core + SQLite | P0 | S | T006 | DONE (TodoRepository over `IDbContextFactory<AppDbContext>`, short-lived context per call; 5 tests on in-memory SQLite. Registered `ITodoRepository → TodoRepository` in AddInfrastructure) |
| T046 | Todo ViewModel: observable list, add/complete/delete commands, filter (All/Active/Completed) | P0 | M | T045 | DONE (TodoViewModel, CommunityToolkit.Mvvm; talks **only** to ITodoRepository. Add/ToggleComplete/Delete/Load commands, `TodoFilter` enum, in-memory master list projected to a filtered `Items`; 9 tests) |
| T047 | Todo View: inline add field, checkboxes, due date picker, delete button | P0 | M | T046 | DONE (inline add TextBox + Enter key-binding, DatePicker for due date, ComboBox filter selector, per-row CheckBox→ToggleCompleteCommand + Delete button. Loads on `OnLoaded`. Note: Avalonia 12 `TextBox.Watermark`→`PlaceholderText`) |

---

## Phase 9 — Automation Engine

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T048 | Set up Quartz.NET scheduler in MyWorkHub.App with DI job factory | P0 | S | T002 | DONE |
| T049 | Implement IAutomationLogger with EF Core (AutomationRun entity, BeginRun / CompleteRun) | P0 | S | T006 | DONE |
| T050 | Automation list ViewModel: list all registered jobs, last run status, next scheduled run, "Run now" command | P0 | M | T048, T049 | TODO |
| T051 | Automation list View: job cards with status badges, run history drawer (last 30 runs per job) | P0 | M | T050 | TODO |
| T052 | Set up Microsoft.Playwright: on first run call `await Playwright.InstallAsync()` for Chromium only (do not install Firefox or WebKit). Implement IBrowserService / IBrowserSession wrapper around Playwright's IPage | P0 | L | T002 | TODO |

---

## Phase 10 — Transport Reimbursement Automation

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T053 | Playwright TCL script: login, navigate to invoice list, identify current month invoice, download PDF | P0 | XL | T052 | TODO |
| T054 | Email template engine: string interpolation for {{subscription_start}} and {{subscription_end}} variables | P0 | S | T001 | TODO |
| T055 | Implement GraphEmailService.SendEmailAsync — the method is already declared in IEmailService; this task is implementing it using the Graph SDK (send mail endpoint, with optional file attachment from local path) | P0 | M | T013 | TODO |
| T056 | TransportReimbursementJob: orchestrate TCL download → build email from template → send via Graph → log result | P0 | M | T053, T054, T055, T049 | TODO |
| T057 | Register TransportReimbursementJob in Quartz.NET with cron from config | P0 | S | T048, T056 | TODO |

---

## Phase 11 — Remote Work Sync Automation

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T058 | Remote Work ViewModel: month/year selector, calendar day picker, load/save RemoteWorkSchedule from SQLite | P0 | M | T006 | TODO |
| T059 | Remote Work View: calendar grid showing days of the month, toggle remote/office per day, sync status per system | P0 | L | T058 | TODO |
| T060 | Playwright PeopleNet script: login, navigate to remote work declaration, fill in selected days, submit | P0 | XL | T052 | TODO |
| T061 | Playwright mwork script: login, navigate week-by-week through target month, mark each remote day, save | P0 | XL | T052 | TODO |
| T062 | RemoteWorkSyncJob: orchestrate PeopleNet → Outlook → mwork steps (independent, each logged separately) | P0 | M | T060, T015, T061, T049 | TODO |
| T063 | Update RemoteWorkSchedule.SyncStatusJson with per-system result after sync | P1 | S | T062 | TODO |

---

## Phase 12 — Settings UI

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T064 | Settings ViewModel + View: tabbed layout (Integrations / Automations / Preferences) | P0 | M | T021 | DONE (SettingsView TabControl; Integrations built, Automations placeholder, Preferences has a live Theme selector) |
| T065 | Azure DevOps tab: PAT field (masked), organization URL, project list (add/remove) | P0 | S | T064 | DONE (masked PAT → ICredentialStore AZDO_PAT; org URL + projects → appsettings.json via IAzureDevOpsSettingsService, which also updates the live AzureDevOpsOptions so PR/Work Items pick it up without restart) |
| T066 | Credentials tab: TCL username/password, PeopleNet URL/username/password, mwork username/password — all masked, saved via ICredentialStore | P0 | M | T064, T005 | TODO |
| T067 | Automation tab: facturation email, email subject template, email body template, cron expression, enabled toggle | P0 | M | T064 | TODO |
| T068 | Preferences tab: refresh interval, minimize to tray toggle | P2 | S | T064 | TODO |
| T069 | First-run wizard: detect missing required settings, guide user through minimum config before showing dashboard | P1 | M | T064 | TODO |

> **Phase 6/7/12 implementation notes (2026-06-22, Graph-independent build):**
> - **End-to-end AzDO config works without restart.** `IAzureDevOpsSettingsService` (Infra) writes the PAT to the DPAPI store and the org URL + projects to the user's `appsettings.json` (`UserAppSettingsFile`, JsonNode DOM so other sections survive), **and** mutates the shared `AzureDevOpsOptions` instance. Since `AzureDevOpsService` reads that same `IOptions` value and the PAT per-request, saving in Settings makes the PR/Work Items pages use the new values immediately.
> - **Missing-PAT UX:** PR and Work Items VMs check `ICredentialStore` for `AZDO_PAT`; when absent they show "Configure your Azure DevOps PAT in Settings" instead of calling the API. Other failures (network/HTTP) show a generic error and log via `[LoggerMessage]`.
> - **All AzDO/settings VM dependencies are optional (nullable, `= null`)** — same pattern as the dashboard — so the `AddUi()`-only test/nav harness still resolves them (Infra services aren't registered there). `IBrowserLauncher` is a UI-layer service (`BrowserLauncher`, shell-execute) registered in `AddUi`.
> - **Core `WorkItem` gained an `Effort` field** (trailing defaulted positional param, non-breaking) mapped from StoryPoints/Effort — §7 didn't include it but T042 needs it.
> - **Verified against the live Cegid org (2026-06-22).** Probed `connectionData` with Hicham's stored PAT (valid, returns user id `c2135ba4-…`).
> - **BUGFIX — `connectionData` 400.** `GetCurrentUserIdAsync` requested `_apis/connectionData?api-version=7.1`, but that resource is **preview-only** — the server returns 400 *"the -preview flag must be supplied… e.g. 7.1-preview"*. Since it's the first call, it blocked the whole PR/Work Items load. Fixed to `7.1-preview.1` (GA endpoints git/wit stay `7.1`). Also: `SendAsync` now reads the response body on non-success and includes it in the thrown exception so AzDO's own error message reaches the log.
> - **Empty projects guard:** with a PAT set but `AzureDevOps:Projects` empty, the PR/Work Items pages now prompt *"Add at least one Azure DevOps project in Settings"* instead of showing a silent empty list.
> - **Team PRs in the review queue (2026-06-22).** `GetPullRequestsForReviewAsync` now also returns PRs where a team Hicham belongs to is a requested reviewer. Teams are auto-discovered via `_apis/teams?$mine=true` (`api-version=7.1-preview.3`, verified live) and each team is queried in **its own project** (`projectName` from the teams response), so team PRs surface even if that project isn't in the configured `Projects` list. Results are merged + deduped by (project, PR id); a direct personal review wins over a "via team" entry. `PullRequestItem.ReviewerTeam` carries the team name → the UI shows a "via &lt;team&gt;" badge. Team lookup is best-effort (failure logs a warning and falls back to the personal queue only). Vote falls back to the team's aggregate vote when the user hasn't personally voted.
> - **Field note:** Hicham is on team **"Platform DevX"** (project **Retail**); his personal review queue also lives in **Retail**, which is not yet in `Projects` — adding it will populate the personal queue.
> - **Dashboard work-item count (2026-06-22).** The card showed *overdue/due-today* count, which is almost always 0 because AzDO work items rarely carry a `DueDate` (sprints use iteration). Changed the headline number to the **total** current-sprint items assigned to the user (`WorkItemCount`), with an "{n} overdue" red sub-line shown only when there are due-dated overdue items. `WorkItemsAttentionCount` (overdue) is retained.
> - **BUGFIX — Work Items always empty (2026-06-22).** The WIQL filters `[System.IterationPath] = @CurrentIteration`, but **`@CurrentIteration` is a team-relative macro** — it only resolves when the query is POSTed to a **team-scoped** wiql endpoint. The app posted to the project-scoped `…/{project}/_apis/wit/wiql`, so the macro resolved to nothing → 0 rows (verified live: project-scope=0, team-scope `Retail/Platform DevX`=2, assigned-to-me-without-iteration=22). Fix: `RunWiqlAsync` now targets `…/{project}/{team}/_apis/wit/wiql`, resolving the team per project as the user's own team (from `teams?$mine=true`) and falling back to the project's **default team** (`GET _apis/projects/{project}` → `defaultTeam`). Results deduped by work-item id.

---

## Cross-Cutting

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| T070 | Global error boundary in UI: catch unhandled ViewModel exceptions, show friendly error with "View log" link | P1 | S | T026 | TODO |
| T071 | App icon design and Avalonia asset registration | P2 | S | T003 | TODO |
| T072 | Write MyWorkHub.Core.Tests: unit tests for template engine, TodoItem logic, RemoteWorkSchedule day selection | P1 | M | T054, T045, T058 | TODO |
| T073 | README.md: setup instructions (Azure AD app registration steps, first-run config) | P1 | S | T010 | TODO |

---

## V2 Backlog

| ID | Task | Priority | Effort | Dependencies | Status |
|----|------|----------|--------|--------------|--------|
| TV001 | Vacation workflow: guided checklist UI (PeopleNet → manager → Excel) | V2 | L | T021 | TODO |
| TV002 | Vacation workflow: Playwright PeopleNet vacation request script | V2 | XL | T052 | TODO |
| TV003 | Excel quick-fill: template library UI (add/edit/delete templates with field mappings) | V2 | L | T021 | TODO |
| TV004 | Excel quick-fill: ClosedXML integration, fill named fields, save and open | V2 | M | TV003 | TODO |
| TV005 | ICalendarService fallback: PlaywrightOutlookService (browser-based fallback if Graph is blocked by tenant) | V2 | XL | T052 | TODO |
| TV006 | IEmailService fallback: PlaywrightOutlookService (browser-based fallback if Graph is blocked by tenant) | V2 | XL | T052 | TODO |

---

## Summary

| Phase | Tasks | Effort estimate |
|-------|-------|----------------|
| Phase 0 — Foundation | 9 | ~2 days |
| Phase 1 — Graph integration | 7 | ~2 days |
| Phase 2 — Azure DevOps | 4 | ~1 day |
| Phase 3 — UI Shell | 6 | ~1.5 days |
| Phase 4 — Dashboard | 5 | ~1.5 days |
| Phase 5 — Email Digest | 4 | ~1 day |
| Phase 5b — Teams Digest | 5 | ~1 day |
| Phase 6 — PR Queue | 5 | ~1 day |
| Phase 7 — Work Items | 4 | ~1 day |
| Phase 8 — Personal Todo | 3 | ~0.5 days |
| Phase 9 — Automation Engine | 5 | ~1.5 days |
| Phase 10 — Transport automation | 5 | ~2.5 days |
| Phase 11 — Remote Work sync | 6 | ~3 days |
| Phase 12 — Settings UI | 6 | ~2 days |
| Cross-cutting | 4 | ~1 day |
| **MVP Total** | **78** | **~23 dev-days** |
| V2 | 6 | ~5+ days |
