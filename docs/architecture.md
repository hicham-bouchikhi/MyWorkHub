# MyWorkHub — Architecture Document

**Version:** 1.0  
**Date:** 2026-06-22  
**Status:** Approved — ready for implementation

---

## 1. Solution Overview

MyWorkHub is a Windows desktop application built with Avalonia UI and .NET 10. It follows a layered architecture with clear separation between UI, business logic, and infrastructure. All external integrations (Microsoft Graph, Azure DevOps, browser automation) are hidden behind interfaces defined in the Core project, making them independently testable and swappable.

---

## 2. Solution Structure

```
MyWorkHub/
├── src/
│   ├── MyWorkHub.App/               # Entry point, DI composition root, Avalonia bootstrap
│   ├── MyWorkHub.UI/                # Avalonia Views, ViewModels, Styles, Assets
│   ├── MyWorkHub.Core/              # Domain models, service interfaces, business logic
│   ├── MyWorkHub.Infrastructure/    # All external integrations (Graph, AzDO, Playwright, SQLite)
│   └── MyWorkHub.Automation/        # Quartz.NET jobs, automation workflow orchestration
├── tests/
│   ├── MyWorkHub.Core.Tests/        # Unit tests for business logic
│   └── MyWorkHub.Infrastructure.Tests/  # Integration tests for external services
├── docs/
│   ├── prd.md
│   ├── architecture.md
│   ├── tasks.md
│   └── adr/                        # Architecture Decision Records
├── README.md
├── CLAUDE.md
└── MyWorkHub.slnx
```

---

## 3. Project Responsibilities

### MyWorkHub.App
- Application entry point (`Program.cs`)
- Avalonia app bootstrap (`App.axaml.cs`)
- Dependency injection composition root (all registrations)
- `appsettings.json` loading
- Quartz.NET scheduler startup/shutdown

### MyWorkHub.UI
- Avalonia Views (`.axaml` + code-behind)
- ViewModels (CommunityToolkit.Mvvm `ObservableObject`)
- Navigation service
- App styles and theme overrides
- No direct calls to infrastructure — all through ViewModels → Core interfaces

### MyWorkHub.Core
- Domain models (entities, DTOs, enums)
- Service interfaces (`IEmailService`, `ICalendarService`, `IAzureDevOpsService`, etc.)
- Business logic that does not depend on infrastructure
- No Avalonia, no HTTP clients, no database — pure C#

### MyWorkHub.Infrastructure
- Implementations of all Core interfaces
- Microsoft Graph SDK integration
- Azure DevOps REST client integration
- Microsoft.Playwright browser automation
- EF Core + SQLite (DbContext, migrations, repositories)
- Windows DPAPI credential store
- No UI references

### MyWorkHub.Automation
- Quartz.NET job classes implementing `IJob`
- Workflow orchestrators that sequence steps across multiple services
- Automation run logging
- References Core interfaces only — no direct infrastructure dependencies

---

## 4. Layer Diagram

```
┌─────────────────────────────────────────────────┐
│                  MyWorkHub.UI                       │
│         Views  │  ViewModels  │  Styles          │
│         (Avalonia / CommunityToolkit.Mvvm)       │
└────────────────────┬────────────────────────────┘
                     │ ICommand / CommunityToolkit.Mvvm bindings
                     ▼
┌─────────────────────────────────────────────────┐
│                 MyWorkHub.Core                      │
│   Interfaces  │  Models  │  Business Logic       │
│  IEmailService  ICalendarService  IAzDoService   │
│  ICredentialStore  ITodoRepository               │
└───────────┬─────────────────────────────────────┘
            │ Implemented by
            ▼
┌─────────────────────────────────────────────────┐
│             MyWorkHub.Infrastructure                │
│  GraphEmailService  │  GraphCalendarService      │
│  AzureDevOpsService │  PlaywrightService         │
│  SqliteTodoRepo     │  DpapiCredentialStore       │
│  AppDbContext (EF)  │  CachedDataService         │
└─────────────────────────────────────────────────┘
            ▲
            │ Uses interfaces
┌─────────────────────────────────────────────────┐
│             MyWorkHub.Automation                    │
│  TransportReimbursementJob                       │
│  RemoteWorkSyncJob                               │
│  AutomationRunLogger                             │
│  (Quartz.NET IJob implementations)               │
└─────────────────────────────────────────────────┘
```

---

## 5. Tech Stack

| Component | Package | Version |
|-----------|---------|---------|
| Runtime | .NET | 10 (LTS) |
| UI framework | Avalonia | 12.x |
| MVVM | CommunityToolkit.Mvvm | latest |
| Microsoft Graph | Microsoft.Graph | 6.x (Kiota-based) |
| MSAL auth | Microsoft.Identity.Client | 4.x |
| Azure DevOps | Raw HttpClient REST (no SDK — avoids SqlClient CVE) | — |
| Browser automation | Microsoft.Playwright | 1.x |
| Scheduler | Quartz.NET | 3.x |
| ORM | Microsoft.EntityFrameworkCore | 10.x |
| SQLite driver | Microsoft.EntityFrameworkCore.Sqlite | 10.x |
| Excel (V2) | ClosedXML | latest |
| DI container | Microsoft.Extensions.DependencyInjection | 10.x |
| Configuration | Microsoft.Extensions.Configuration | 10.x |
| Logging | Microsoft.Extensions.Logging + Serilog | latest |

---

## 6. Service Interfaces (Core)

```csharp
// Email
public interface IEmailService
{
    Task<IReadOnlyList<EmailItem>> GetImportantEmailsAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MailFolder>> GetMailFoldersAsync(CancellationToken ct = default);
    Task<string> GetEmailBodyAsync(string id, CancellationToken ct = default);
    Task SendEmailAsync(string to, string subject, string body, string? attachmentPath = null, CancellationToken ct = default);
}

// Email settings (folder picker)
public interface IEmailSettingsService
{
    IReadOnlyList<string> GetWatchedFolderIds();
    void Save(IReadOnlyList<string> folderIds);
}

// AI email digest
public interface IEmailSummaryService
{
    Task<string> SummarizeAsync(IReadOnlyList<EmailItem> emails, CancellationToken ct = default);
}

// Calendar
public interface ICalendarService
{
    Task CreateRemoteWorkEventsAsync(IEnumerable<DateOnly> dates, CancellationToken ct = default);
    Task<IReadOnlyList<DateOnly>> GetExistingRemoteWorkDatesAsync(int year, int month, CancellationToken ct = default);
}

// Azure DevOps
public interface IAzureDevOpsService
{
    Task<IReadOnlyList<PullRequestItem>> GetPullRequestsForReviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> GetMyWorkItemsAsync(CancellationToken ct = default);
    // Best-effort: items whose comments cannot be fetched are silently skipped.
    Task<IReadOnlyList<WorkItemMention>> GetWorkItemMentionsAsync(
        IReadOnlyList<WorkItem> workItems, CancellationToken ct = default);
}

// Teams
public interface ITeamsService
{
    Task<IReadOnlyList<TeamsChatItem>> GetUnreadChatsAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
}

// Todo (local)
public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(TodoItem item, CancellationToken ct = default);
    Task UpdateAsync(TodoItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// Credential store
public interface ICredentialStore
{
    void Save(string key, string plaintext);
    string? Get(string key);
    void Delete(string key);
}

// Work item @mention read-state
public interface ISeenMentionRepository
{
    Task<IReadOnlySet<int>> GetSeenCommentIdsAsync(CancellationToken ct = default);
    Task MarkSeenAsync(int commentId, CancellationToken ct = default);
}

// Code-review prerequisites and execution
public sealed record PrerequisiteCheckResult(bool GitFound, bool ClaudeFound, string? WorkFolderIssue);

public interface IPrReviewService
{
    Task<PrerequisiteCheckResult> CheckPrerequisitesAsync();
    Task<string> ReviewAsync(PullRequestItem pr, IProgress<string>? progress = null, CancellationToken ct = default);
}

// Workspace settings (Claude CLI path, work folder, review model)
public interface IWorkspaceSettingsService
{
    WorkspaceSettings Get();
    void Save(string workFolderPath, string claudeExecutablePath, string reviewModelId);
}

// Automation run logging
public interface IAutomationLogger
{
    Task<Guid> BeginRunAsync(string jobName, CancellationToken ct = default);
    Task CompleteRunAsync(Guid runId, bool success, string? errorMessage = null, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRun>> GetHistoryAsync(string jobName, int count = 30, CancellationToken ct = default);
}

// Browser session (Playwright wrapper)
public interface IBrowserSession : IAsyncDisposable
{
    Task NavigateAsync(string url);
    Task<string> GetTextAsync(string selector);
    Task ClickAsync(string selector);
    Task FillAsync(string selector, string value);
    // Triggers a file download by clicking linkSelector, waits for the download to complete,
    // saves the file to the app temp folder (%USERPROFILE%\.MyWorkHub\temp\), and returns
    // the absolute file path. Do NOT return byte[] — Playwright downloads require a listener
    // registered before the click; the file is always written to disk first.
    Task<string> DownloadFileAsync(string linkSelector);
    Task WaitForSelectorAsync(string selector);
}

public interface IBrowserService
{
    Task<IBrowserSession> CreateSessionAsync(bool headless = true);
}
```

---

## 7. Data Models

### SQLite Entities (EF Core)

```csharp
// Personal task list
public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// Automation execution history
public class AutomationRun
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public AutomationStatus Status { get; set; }  // Pending, Success, Failed, Cancelled
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }  // JSON — step-by-step result details
}

// Encrypted credential storage
public class AppCredential
{
    [Key]  // Key is the EF Core primary key — string PK, not a generated int/Guid
    public string Key { get; set; } = "";          // e.g. "TCL_PASSWORD", "PEOPLENET_PASSWORD"
    public string EncryptedValue { get; set; } = ""; // DPAPI-encrypted, base64-encoded
    public DateTime UpdatedAt { get; set; }
}

// Remote work plans (source of truth for sync)
public class RemoteWorkSchedule
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string DaysJson { get; set; } = "[]";   // JSON array of day numbers, e.g. [2, 5, 9, 12]
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? SyncStatusJson { get; set; }    // JSON: { "peoplenet": "ok", "outlook": "ok", "mwork": "failed" }
}

// Tracks which AzDO work item comment @mentions the user has already opened (PK = CommentId)
public sealed class SeenWorkItemMention
{
    public int CommentId { get; set; }   // PK — ValueGeneratedNever
    public DateTime SeenAt { get; set; }
}
```

### Read Models / DTOs (Core, not persisted)

```csharp
public record EmailItem(string Id, string From, string Subject, string Preview,
    DateTime ReceivedAt, bool IsFlagged, string FolderName = "")
{
    public string? FullBody { get; init; }  // fetched lazily before AI summarisation
}

public record TeamsChatItem(string ChatId, string ChatName, string LastSenderName,
    string MessagePreview, DateTime ReceivedAt, int UnreadCount, string DeepLinkUrl);

public record PullRequestItem(int Id, string Title, string Author, string Repository,
    DateTime CreatedAt, string VoteStatus, string Url);

// Effort = StoryPoints/Effort field. Project = team project name (required for mention queries).
public record WorkItem(int Id, string Title, string Type, string State, string Priority,
    DateTime? DueDate, string Url, string Effort = "", string Project = "");

// A comment on an active work item that @mentions the current user.
public record WorkItemMention(int WorkItemId, string WorkItemTitle, string WorkItemUrl,
    int CommentId, string AuthorDisplayName, DateTime CreatedAt, string TextSnippet);

// Projected for the email folder picker in Settings.
public record MailFolder(string Id, string DisplayName, bool IsWatched);

// Workspace settings for PR code review (Claude CLI path, work folder, review model).
public sealed record WorkspaceSettings(string WorkFolderPath, string ClaudeExecutablePath, string ReviewModelId);
```

---

## 8. Authentication Strategy

### Microsoft Graph (email + calendar)

- **Library:** MSAL.NET (`Microsoft.Identity.Client`)
- **Flow:** Interactive browser popup on first run → token cached to disk (encrypted). Use `WithUseEmbeddedWebView(false)` so MSAL opens the system browser (Edge/Chrome) rather than an embedded WebView2 — this handles Cegid's SSO/conditional access correctly and works without WebView2 being installed
- **App registration:** Personal Azure subscription — multi-tenant app, delegated permissions only
- **Permissions requested:**
  - `Mail.Read` — read inbox
  - `Calendars.ReadWrite` — create Working Elsewhere events
  - `Chat.Read` — read Teams DMs and group chats (user-consentable, no admin needed)
  - `ChannelMessage.Read.All` — read @mentions in channels. **NOT requested at sign-in** (confirmed 2026-06-22): it requires admin consent in the Cegid tenant, and MSAL bundles all scopes into one interactive call, so including it throws an "admin approval required" wall over the *entire* sign-in. The Teams module degrades gracefully without it (DMs/group chats via `Chat.Read`; channel @mentions unavailable). Request incrementally only if/when an admin grants it.
  - `offline_access` — refresh token
  - `User.Read` — basic profile (for user ID)
- **Token cache:** `~/.MyWorkHub/msal_token_cache.bin` — protected with DPAPI
- **Tenant:** `common` endpoint → user signs in with work account `@cegid.com`

### Azure DevOps

- **Method:** Personal Access Token (PAT)
- **Storage:** Encrypted via `ICredentialStore` (DPAPI)
- **Scope required:** `Code (Read)`, `Work Items (Read)`
- **Organization:** `https://dev.azure.com/cegid/`

### External websites (TCL, PeopleNet, mwork)

- **Method:** Stored username/password, automated via Playwright
- **Storage:** Encrypted via `ICredentialStore` (DPAPI) — `TCL_USER`, `TCL_PASS`, `PEOPLENET_USER`, `PEOPLENET_PASS`, `MWORK_USER`, `MWORK_PASS`
- **First-time setup:** Settings UI with password fields → saved encrypted on Save

---

## 9. Automation Engine Design

### Scheduler

Quartz.NET runs embedded in the app process. Jobs are registered at startup with their cron triggers from `AppSettings.Automation.*`.

```
App startup
  └── ISchedulerFactory.CreateScheduler()
  └── Register jobs from DI container
  └── scheduler.Start()

App shutdown
  └── scheduler.Shutdown(waitForJobsToComplete: true)
```

### Job Structure

Each automation implements `Quartz.IJob` and is resolved from DI (Quartz.NET supports DI injection via `JobFactory`).

```
TransportReimbursementJob
  ├── Step 1: IBrowserService.CreateSessionAsync()
  │    └── PlaywrightTclService.DownloadInvoiceAsync()
  ├── Step 2: IEmailService.SendEmailAsync(to, subject, body, attachment)
  ├── Step 3: Log result via IAutomationLogger
  └── Step 4: Dispose browser session

RemoteWorkSyncJob  (manual trigger only — no cron)
  ├── Step 1: Get selected dates from RemoteWorkSchedule
  ├── Step 2: ICalendarService.CreateRemoteWorkEventsAsync(dates)   [Outlook]
  ├── Step 3: PlaywrightPeopleNetService.FillRemoteDaysAsync(dates)
  ├── Step 4: PlaywrightMworkService.FillRemoteDaysAsync(dates)
  └── Step 5: Log per-step result, update RemoteWorkSchedule.SyncStatusJson
  
  IMPORTANT: Steps 2, 3, and 4 are executed independently with individual try/catch blocks.
  A failure in one step (e.g. PeopleNet) must NOT prevent the other steps (Outlook, mwork)
  from running. All three steps always execute; results are collected and logged per step.
```

### Retry Policy

Each step retries up to 3 times with 30-second delays before marking as failed. Steps are independent — a PeopleNet failure does not prevent the Outlook step from running.

---

## 10. Configuration Schema

`appsettings.json` — non-sensitive values only. Sensitive values (passwords, tokens) are stored in DPAPI via `ICredentialStore` and only referenced by key.

```json
{
  "AzureAD": {
    "ClientId": "<app-client-id>",
    "TenantId": "common"
  },
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/cegid/",
    "Projects": ["project-name-1", "project-name-2"]  // AzDO team project names as they appear in the URL: dev.azure.com/cegid/{project-name}
  },
  "Automation": {
    "TransportReimbursement": {
      "Enabled": true,
      "CronExpression": "0 0 9 1 * ?",
      "FacturationEmail": "",
      "EmailSubjectTemplate": "Remboursement titre de transport - {{subscription_start}} au {{subscription_end}}",
      "EmailBodyTemplate": ""
    }
  },
  "ExternalSites": {
    "TCL": { "BaseUrl": "https://www.tcl.fr/" },
    "PeopleNet": { "BaseUrl": "" },
    "MWork": { "BaseUrl": "https://app.m-work.co" }
  },
  "UI": {
    "RefreshIntervalMinutes": 5,
    "MinimizeToTrayOnClose": true,
    "Theme": "System",
    "Palette": "GitHub"
  },
  "Workspace": {
    "WorkFolderPath": "",          // folder where PR repos are cloned for review
    "ClaudeExecutablePath": "",    // empty = resolve "claude" from PATH
    "ReviewModelId": "claude-sonnet-5"
  },
  "Email": {
    "FolderIds": [ "inbox" ],      // well-known names or Graph folder IDs
    "MaxPerFolder": 25
  }
}
```

---

## 11. Error Handling Strategy

| Scenario | Handling |
|----------|----------|
| Graph API call fails | Log warning, show stale data with "last updated" timestamp |
| AzDO API call fails | Log warning, show stale data |
| Automation step fails | Retry 3× with 30s delay, then mark run as Failed, show toast notification |
| Playwright can't find element | Screenshot the page, save to `~/.MyWorkHub/errors/`, log with details |
| MSAL token expired | Silently refresh via refresh token; re-prompt if refresh fails |
| Network offline | Show offline indicator on dashboard, serve cached data |

---

## 12. Implementation Notes for AI

These notes exist because the docs above contain choices that are easy to implement incorrectly. Read before scaffolding.

| Topic | Correct approach |
|-------|-----------------|
| MSAL browser | Always use `WithUseEmbeddedWebView(false)` — opens system browser, not WebView2 |
| Playwright browser | Install and use **Chromium only** (`playwright install chromium`) — do not install Firefox or WebKit |
| Playwright downloads | Register `page.Download += handler` BEFORE clicking the download link; do not try to intercept network responses |
| EF Core migrations | Apply via `dbContext.Database.MigrateAsync()` on startup — never require the user to run `dotnet ef` manually |
| AppCredential PK | `Key` (string) is the primary key — annotate with `[Key]` and do NOT add a separate `Id` column |
| DI registration order | Register `ICredentialStore` (DpapiCredentialStore) before anything that uses credentials (MSAL setup, AzDO client) |
| RemoteWorkSyncJob steps | Wrap each of the 3 sync steps (Outlook, PeopleNet, mwork) in its own `try/catch` — never let one failure abort the others |
| ITeamsService.GetUnreadCountAsync | This is a lightweight call for the dashboard counter only. Implement it by summing `unreadMessageCount` across `/me/chats` — do NOT call `GetUnreadChatsAsync` and count the results (that fetches message previews unnecessarily) |
| Template variables | `{{subscription_start}}` and `{{subscription_end}}` are the exact variable tokens in the email template — use these exact strings in the template engine, do not change the format |
| AzDO `Projects` config | Values are team project names as they appear in the URL path: `dev.azure.com/cegid/{ProjectName}` |

---

## 13. Folder Layout at Runtime

```
%USERPROFILE%\.MyWorkHub\
├── MyWorkHub.db                   # SQLite database
├── appsettings.json                  # User configuration
├── msal_token_cache.bin              # Encrypted MSAL token cache
├── logs\
│   └── MyWorkHub-YYYY-MM-DD.log  # Serilog rolling file
├── temp\
│   └── tcl-invoice-YYYY-MM.pdf      # Downloaded TCL invoice (deleted after email sent)
└── errors\
    └── playwright-YYYYMMDD-HHmmss.png  # Playwright failure screenshots
```
