# MyWorkHub

Windows desktop app (.NET 10 / Avalonia 12) that consolidates a developer's daily workflow into a single dashboard.

## What it does

| Module | Description |
|--------|-------------|
| Dashboard | Aggregates email, Teams, PR, work items, todo, and automation status at a glance |
| Email digest | Unread + flagged Outlook messages; AI summarisation via Claude CLI |
| PR review queue | Azure DevOps PRs waiting for your review; inline Claude Code AI review |
| Work items | Current-sprint AzDO items with @mention comment notifications |
| Teams | Unread DM and group chat previews |
| Personal todo | Local-only SQLite task list |
| Automations | Transport reimbursement (TCL → finance email) and remote-work sync (PeopleNet + Outlook + mwork) |

## Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A personal Azure subscription for the AD app registration (free tier is sufficient)
- Optional: [Claude CLI](https://claude.ai/download) logged in — required for AI email summary and PR code review

## Setup

### 1. Azure AD app registration

1. Go to [portal.azure.com](https://portal.azure.com) → **Azure Active Directory → App registrations → New registration**
2. Name: `MyWorkHub`, Supported account types: **Accounts in any organizational directory**
3. No redirect URI needed (MSAL uses the system browser)
4. Under **API permissions → Add a permission → Microsoft Graph → Delegated permissions**, add:
   - `Mail.Read`
   - `Calendars.ReadWrite`
   - `Chat.Read`
   - `User.Read`
   - `offline_access`
   - **Do not add `ChannelMessage.Read.All`** — it requires tenant admin consent and will block the entire sign-in flow
5. Copy the **Application (client) ID** into `~/.MyWorkHub/appsettings.json` → `AzureAD.ClientId`
6. Set `AzureAD.TenantId` to your Cegid tenant ID (or `"common"` for multi-tenant)

### 2. Azure DevOps PAT

1. Go to `dev.azure.com/cegid` → **User settings → Personal access tokens → New token**
2. Scopes: **Code (Read)**, **Work Items (Read)**
3. Enter the PAT in the app under **Settings → Integrations → Azure DevOps**

### 3. First run

```bash
dotnet run --project src/MyWorkHub.App
```

On first launch the app seeds `%USERPROFILE%\.MyWorkHub\appsettings.json` from the embedded template and runs EF Core migrations automatically. Sign in with your `@cegid.com` account when the browser opens.

### 4. Optional: AI features (Claude CLI)

Install the [Claude CLI](https://claude.ai/download) and log in (`claude auth login`). Then set the path in **Settings → Preferences → Workspace** if `claude` is not on your PATH.

## Build & test

```bash
# Solution-wide — always -m:1 (parallel restore crashes on .slnx)
dotnet build MyWorkHub.slnx -m:1
dotnet test  MyWorkHub.slnx -m:1

# Run the app
dotnet run --project src/MyWorkHub.App
```

## Runtime data

All runtime data lives under `%USERPROFILE%\.MyWorkHub\`:

```
~/.MyWorkHub/
├── MyWorkHub.db          # SQLite — todos, automation history, credentials, seen mentions
├── appsettings.json         # Non-sensitive config (edit here, not in src/)
├── msal_token_cache.bin     # DPAPI-encrypted MSAL token cache
├── review-agent.md          # Claude Code PR review instructions (seeded on first run)
├── logs/                    # Serilog rolling logs
├── temp/                    # Temporary downloads (deleted after use)
└── errors/                  # Playwright failure screenshots
```

## Documentation

- [`docs/prd.md`](docs/prd.md) — product requirements
- [`docs/architecture.md`](docs/architecture.md) — system design, interfaces, data models
- [`docs/tasks.md`](docs/tasks.md) — task matrix and implementation status
- [`docs/adr/`](docs/adr/) — architecture decision records
