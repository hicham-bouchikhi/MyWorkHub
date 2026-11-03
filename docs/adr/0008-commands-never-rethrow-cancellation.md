# ADR-0008: Page commands absorb superseded cancellations and report all others

**Status:** Accepted  
**Date:** 2026-10-03

## Context

The app crashed twice with an unhandled `TaskCanceledException` from a page load:

1. MSAL's token-cache read was cancelled internally. `GraphPageViewModel.ReloadAsync` excluded every `OperationCanceledException` from its error handling (`when (ex is not OperationCanceledException)`), assuming a cancellation is always the user's.
2. Navigating back to the Email page while its first load was still running. CommunityToolkit's `AsyncRelayCommand` **cancels the previous execution's token when the command is executed again**, and then rethrows whatever escapes the execution to the dispatcher (`AwaitAndThrowIfFailed`), which terminates the app.

So both "someone else cancelled" and "we cancelled" ended in a crash.

## Decision

A page command never lets an `OperationCanceledException` escape:

```csharp
catch (OperationCanceledException) when (ct.IsCancellationRequested)
{
    return; // superseded: the newer execution owns the page
}
catch (Exception ex)
{
    ReportFailure(ex); // includes cancellations the token did not request (timeouts, library internals)
}
```

Applied to `GraphPageViewModel.ReloadAsync` (Email, Calendar, Teams), `AzureDevOpsPageViewModel.ReloadAsync` (Pull requests, Work items), `EmailViewModel.LoadFolderAsync` and `EmailSettingsViewModel.LoadFoldersAsync`.

## Consequences

- A superseded load ends quietly; the newer one shows the data. A timeout or library-internal cancellation shows as an error on the page instead of killing the app.
- Regression tests pin both paths (`Should_not_fault_when_a_running_load_is_superseded_by_a_new_one`, `Should_show_an_error_when_a_library_cancels_on_its_own`) for the Graph and the Azure DevOps base pages.
- A superseded run's `finally` still clears the shared busy flag, so the progress indicator can disappear slightly before the newer run finishes.
- The remaining page commands (Todo, Automations, Work-item mentions, the Workspace/Azure DevOps/Email Settings tabs, Dev deep-link tests) were migrated on 2026-11-03, each with a `..._library_cancels_on_its_own` regression test; no page command uses `when (ex is not OperationCanceledException)` any more. Mark-mention-read stays silent on every failure, cancellation included (read state is a convenience).
- The sign-in/connect commands (`GraphPageViewModel.SignInAsync`, `AzureDevOpsPageViewModel.ConnectAsync`, `AzureDevOpsSettingsViewModel.ConnectAsync`) follow the same rule: a timeout or library cancellation shows on the prompt.
- **Per-row concurrent commands take no `CancellationToken`.** `AsyncRelayCommand` cancels the running execution's token on every new execution even with `AllowConcurrentExecutions = true`, so a click on another row would cancel — and, with the rule above, silently abandon — the first row's change. `ToggleCompleted`/`Delete` (Todo), `RunNow` (Automations) and `OpenMention` (Work items) therefore call their service without a token and report every failure.
- Helpers that are not commands (e.g. `EmailViewModel.LoadFoldersAsync`) may still let a cancellation through on purpose, to the command that awaits them, which applies the rule above.
