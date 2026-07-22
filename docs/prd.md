# MyWorkHub — Product Requirements Document

**Version:** 1.1  
**Date:** 2026-06-22  
**Author:** Hicham Bouchikhi (hbouchikhi@cegid.com)  
**Status:** Approved — ready for implementation

---

## 1. Problem Statement

Daily developer work at Cegid is fragmented across Outlook, Azure DevOps, PeopleNet, TCL, mwork, and Excel. There is no single place to see what needs attention right now. Monthly admin tasks (transport reimbursement, remote work scheduling across three systems) require logging into multiple websites and entering the same data repeatedly. Context-switching between tools costs focus and causes tasks to fall through the cracks.

---

## 2. Goals

- One dashboard showing everything requiring action today
- Automate the monthly transport reimbursement end-to-end (TCL → finance email)
- Declare remote work days once and sync automatically to PeopleNet, Outlook, and mwork
- Surface Azure DevOps PRs waiting for review and committed work items without opening a browser
- Reduce repetitive admin clicks — MyWorkHub is a companion to existing tools, not a replacement

## 2.1 Non-Goals

- Not a full email client — read-only digest, no compose/reply
- Not a replacement for Azure DevOps — links out for detail views
- Not shared with a team — strictly personal, local data only
- No mobile support in V1

---

## 3. Users

Single user: Hicham Bouchikhi, developer at Cegid.
- Works on Windows
- Uses Office 365 (hbouchikhi@cegid.com)
- Azure DevOps at https://dev.azure.com/cegid/
- Monthly obligations: transport reimbursement, remote work scheduling
- External accounts: TCL (transport subscription), PeopleNet (HR), mwork (https://app.m-work.co)

---

## 4. Functional Requirements

### 4.1 Dashboard (MVP)

The first screen the user sees when opening MyWorkHub. Aggregates live counts and urgent items from all modules.

**Requirements:**
- Display unread + flagged email count with a preview of the top 3 items
- Display count of PRs waiting for the user's review
- Display Teams unread message count
- Display count of overdue and due-today work items
- Display status of upcoming scheduled automations (next run, last run result)
- Display personal todo items due today
- Auto-refresh all data every 5 minutes
- Each widget is clickable and navigates to its full module view

### 4.2 Email Digest (MVP)

Read-only view of important emails from Office 365 via Microsoft Graph.

**Requirements:**
- List unread and flagged messages from the Inbox
- Show sender, subject, received date, and a 1-line preview
- Support filtering by sender name/email and subject keyword
- Clicking an email opens it in Outlook Web (browser)
- Manual refresh button
- Auto-refresh every 5 minutes

**Out of scope:** Compose, reply, delete, move emails.

### 4.3 PR Review Queue (MVP)

Azure DevOps pull requests where the user is a required or optional reviewer.

**Requirements:**
- List PRs across all configured AzDO projects where the user is a reviewer
- Show: title, author, repository, creation date, age (days), vote status (Approved / Waiting / Rejected / No vote)
- Sort by age descending (oldest first)
- Highlight PRs older than 2 days
- Clicking a PR opens it in Azure DevOps (browser)
- Badge count on the app taskbar icon reflecting pending reviews
- Auto-refresh every 5 minutes

### 4.4 My Work Items (MVP)

Azure DevOps tasks and user stories assigned to the current user.

**Requirements:**
- List work items assigned to the user in the current sprint across all configured projects
- Show: title, type (Task/User Story/Bug), state, priority, effort estimate
- Highlight items that are overdue (past sprint end date or past their due date)
- Show items grouped by state (Active, Resolved, New)
- Clicking an item opens it in Azure DevOps (browser)
- Auto-refresh every 5 minutes

### 4.5 Teams Message Digest (MVP)


Read-only view of pending Teams messages that require a response, sourced from Microsoft Graph.

**Requirements:**
- List chats (1:1 DMs and group chats) where `unreadMessageCount > 0` as returned by the Microsoft Graph `/me/chats` endpoint — do NOT attempt to infer "unanswered" by checking who sent the last message; rely solely on the API's unread count field
- Show: sender of last message, chat name (or "Direct message" for 1:1), message preview, received date
- Unread count badge visible on the dashboard and sidebar
- Clicking a conversation opens it in Microsoft Teams (browser or desktop app)
- Manual refresh button
- Auto-refresh every 5 minutes
- If `ChannelMessage.Read.All` admin consent is unavailable: scope limited to chats/DMs only, channel mentions not shown (graceful degradation)

**Out of scope:** Composing or replying to messages, channel browsing.

### 4.6 Personal Todo (MVP)

Lightweight local-only task list for admin tasks, reminders, and things that do not belong in Azure DevOps.


**Requirements:**
- Add a task with a title and optional due date
- Mark a task as complete
- Delete a task
- Filter: All / Active / Completed
- Tasks due today or overdue appear on the Dashboard
- Data stored locally in SQLite — never synced externally

### 4.7 Automation Engine (MVP)

Infrastructure for running scheduled and manual multi-step workflows that combine browser automation and API calls.

**Requirements:**
- Cron-based scheduler using Quartz.NET
- Each automation has an enabled/disabled toggle
- Each automation can be triggered manually ("Run now")
- All executions are logged: job name, start time, end time, status (Success / Failed), error message
- Run history visible per automation (last 30 runs)
- Failure notifications shown as toast in the app

---

## 5. Automation Routines

### 5.1 Transport Reimbursement (Monthly)

**Trigger:** Configurable cron (default: 1st of every month at 09:00)  
**Purpose:** Download the current month's TCL subscription invoice and email it to the finance/facturation team for reimbursement.

**Steps:**
1. Playwright opens `https://www.tcl.fr/` and logs in with stored credentials
2. Navigates to subscription invoices section
3. Downloads the current month's invoice PDF to `%USERPROFILE%\.MyWorkHub\temp\` (created if it doesn't exist; file deleted after email is sent)
4. Microsoft Graph API composes an email:
   - To: configurable facturation email address
   - Subject: from configurable template with variables `{{subscription_start}}` and `{{subscription_end}}`
   - Body: from configurable template with variables `{{subscription_start}}` and `{{subscription_end}}`
   - Attachment: the downloaded invoice PDF
5. Sends the email via Graph API
6. Deletes the temp PDF file
7. Logs the run result

**Configuration (in Settings UI):**
- Enabled toggle
- Cron expression
- Facturation email address
- Email subject template
- Email body template

### 5.2 Remote Work Scheduling (Monthly, manual trigger)

**Trigger:** Manual — user initiates from the Remote Work module  
**Purpose:** Enter remote work days into PeopleNet, Outlook, and mwork in a single action, avoiding triple data entry.

**Steps:**
1. User opens the Remote Work module in MyWorkHub
2. User selects which days they will work remotely using a calendar picker (current month or any future month)
3. User clicks "Sync all"
4. **Playwright → PeopleNet:** logs in, navigates to the remote work declaration form, fills in the selected days, submits
5. **Microsoft Graph → Outlook:** creates "Working Elsewhere" all-day events for each selected day in the user's calendar (Europe/Paris timezone)
6. **Playwright → mwork (`https://app.m-work.co`):** logs in, navigates to "Mes jours", iterates week by week through the target month, marks each selected day as remote, saves
7. Logs the run result per target system (each step logged independently)

**Configuration (in Settings UI):**
- PeopleNet credentials (stored encrypted)
- mwork credentials (stored encrypted)

---

## 6. Configuration Requirements

All sensitive values (credentials, tokens, email addresses) must be configurable through the Settings UI — no hardcoded values in code. Credentials are encrypted at rest using Windows DPAPI.

| Setting | Module | Required |
|---------|--------|----------|
| Azure AD Client ID | Graph auth | Yes |
| Azure DevOps PAT | AzDO | Yes |
| Azure DevOps project(s) | AzDO | Yes |
| TCL username | Transport automation | Yes |
| TCL password | Transport automation | Yes (encrypted) |
| PeopleNet URL | Remote work | Yes |
| PeopleNet username | Remote work | Yes |
| PeopleNet password | Remote work | Yes (encrypted) |
| mwork username | Remote work | Yes |
| mwork password | Remote work | Yes (encrypted) |
| Facturation email address | Transport automation | Yes |
| Email subject template | Transport automation | Yes |
| Email body template | Transport automation | Yes |
| Transport automation cron | Transport automation | Yes |

---

## 7. Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| Startup time | < 3 seconds to first visible screen |
| API refresh interval | 5 minutes (configurable) |
| Automation reliability | Retry up to 3 times on failure before logging as failed |
| Credential security | All credentials encrypted with Windows DPAPI, never written to disk in plaintext |
| Offline behavior | Show cached data from last successful fetch when offline; indicate stale data |
| Platform | Windows 10/11 only (V1) |
| Runtime | .NET 10 |

---

## 8. V2 Backlog

The following features are explicitly deferred to a future version after MVP is stable.

### 8.1 Vacation Workflow
A guided checklist that walks through the vacation request process:
- Checklist: PeopleNet request → manager approval → Excel template fill
- Deep link to PeopleNet vacation request page
- Auto-fill Excel vacation template (using ClosedXML)

### 8.2 Excel Quick-Fill
A template library for recurring Excel files:
- Store Excel template paths with named field mappings
- Auto-fill date, name, and data fields
- Open the filled result in Excel

---

## 9. Resolved Decisions

| Question | Decision |
|----------|----------|
| App type | Avalonia UI desktop app (.NET 10, Windows) |
| Auth for Microsoft Graph | MSAL.NET with app registered in personal Azure subscription (multi-tenant), delegated permissions |
| No Azure AD admin at Cegid | Use personal Azure subscription (€150 credits available) — app registration is free |
| Outlook calendar event style | "Working Elsewhere" all-day events (FreeBusyStatus.WorkingElsewhere) |
| mwork URL | https://app.m-work.co |
| Facturation email | Configurable in Settings UI — not hardcoded |
| Credential storage | Windows DPAPI (System.Security.Cryptography.ProtectedData) |
| Graph API fallback | ICalendarService / IEmailService interfaces defined; Playwright-based implementation can be swapped in if tenant blocks the app |
| Teams scope | Intent is to try both Chat.Read (DMs) and ChannelMessage.Read.All (channel @mentions). If ChannelMessage.Read.All is denied by tenant, the app degrades gracefully to DMs only — it does not hard-fail |
