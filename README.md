# MyWorkHub

Single-user desktop app (.NET 10 / Avalonia 12) that consolidates a developer's daily workflow into one window. Windows first; also runs on Linux and macOS.

## What it does

| Page | Description |
|------|-------------|
| Dashboard | At-a-glance stats from every service: mail, Teams, PRs, work items, todo, automations |
| Email | Outlook mail client: folder tree, optional *Favorites* view merging chosen folders, sandboxed HTML reading pane, AI digest of the list or of one message (Claude CLI) |
| Calendar | Upcoming Outlook events |
| Teams | Unread DM and group chat previews |
| Pull requests | Azure DevOps PRs waiting for your review; AI code review by the Claude CLI in a local clone |
| Work items | Current-sprint AzDO items and comments that @mention you |
| Todo | Local-only SQLite task list |
| Automations | Scheduled browser automations: transport reimbursement (TCL → finance email) and remote-work sync (Outlook + PeopleNet + mwork) |
| Settings | Appearance, notifications, workspace, Azure DevOps, email, about |

Toast notifications (with a bell history) announce new mail, chats, PRs, work items, mentions and events on each refresh, and open the matching item when clicked.

## Prerequisites

- Windows 10/11, Linux or macOS
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (only to build from source — [releases](https://github.com/hicham-bouchikhi/MyWorkHub/releases) ship self-contained binaries)
- A Microsoft Entra ID app registration (see below)
- Linux: WebKitGTK (email reading pane) and libsecret (token cache)
- Optional: [Claude CLI](https://claude.ai/download), logged in — required for the email digest and PR review

## Setup

### 1. Entra ID (Azure AD) app registration

1. In [portal.azure.com](https://portal.azure.com) → **Microsoft Entra ID → App registrations → New registration**
2. Name: `MyWorkHub`, supported account types: **Accounts in any organizational directory**
3. No redirect URI needed (MSAL uses the system browser)
4. Under **API permissions → Add a permission → Microsoft Graph → Delegated permissions**, add:
   - `Mail.Read`
   - `Calendars.ReadWrite`
   - `Chat.Read`
   - `User.Read`
   - `offline_access`
   - **Do not add `ChannelMessage.Read.All`** — it requires tenant admin consent and blocks the whole sign-in flow
5. Put the **Application (client) ID** in `~/.MyWorkHub/appsettings.json` → `AzureAD.ClientId`
6. Set `AzureAD.TenantId` to your tenant ID (or `"common"`)

### 2. Azure DevOps PAT

1. In `dev.azure.com/<org>` → **User settings → Personal access tokens → New token**
2. Scopes: **Code (Read)**, **Work Items (Read)**
3. In the app: **Settings → Azure DevOps**, set the organization URL and projects, paste the PAT, then **Verify & save**

The PAT is stored encrypted (Data Protection), never in `appsettings.json`.

### 3. First run

```bash
dotnet run --project src/MyWorkHub.App
```

The first launch seeds `~/.MyWorkHub/appsettings.json` and `review-agent.md` from the shipped copies and applies the database migrations. Sign in with your Microsoft 365 account from any Graph page when the browser opens.

### 4. Optional: AI features (Claude CLI)

Install the [Claude CLI](https://claude.ai/download) and log in. If `claude` is not on your `PATH`, set its location in **Settings → Workspace** — where you also choose the folder PR reviews clone into, the review model and the review-agent template.

## Configuration

Everything lives in `~/.MyWorkHub/appsettings.json` (`%USERPROFILE%\.MyWorkHub\` on Windows) — `src/MyWorkHub.App/appsettings.json` is only the first-run seed. Prefer the **Settings** page: Azure DevOps, Workspace, Email, notification and appearance changes apply immediately (notifications from the next check). Other sections (`AzureAD`, `Automation` schedules, `ExternalSites`, `UI:MinimizeToTrayOnClose`) are read at startup — restart after editing them.

## Build & test

```bash
# Solution-wide — always -m:1 (parallel restore crashes on .slnx)
dotnet build MyWorkHub.slnx -m:1
dotnet test  MyWorkHub.slnx -m:1

# Run the app
dotnet run --project src/MyWorkHub.App
```

The build treats every warning as an error. Pushes to `master` build self-contained single-file releases for Windows, Linux and macOS (x64 + arm64); the version comes from the `VERSION` file.

## Runtime data

```
~/.MyWorkHub/
├── MyWorkHub.db           # SQLite — todos, automation history, encrypted credentials, seen mentions
├── appsettings.json       # Non-secret configuration
├── keys/                  # Data Protection key ring (encrypts the stored credentials)
├── msal_token_cache.bin   # Microsoft 365 token cache, encrypted per OS
├── review-agent.md        # PR review instructions for the Claude CLI
├── repos/                 # PR review clones (default work folder)
├── logs/                  # Serilog rolling logs
├── temp/                  # Temporary downloads
└── errors/                # Automation failure screenshots
```

## Documentation

- [`AGENTS.md`](AGENTS.md) — architecture, conventions and integration details (source of truth together with the code)
- [`docs/adr/`](docs/adr/) — architecture decision records
- [`docs/prd.md`](docs/prd.md), [`docs/architecture.md`](docs/architecture.md), [`docs/tasks.md`](docs/tasks.md) — pre-rewrite specs, still useful for product intent
