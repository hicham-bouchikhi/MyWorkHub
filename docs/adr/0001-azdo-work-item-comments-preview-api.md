# ADR-0001: Use the preview-only Azure DevOps Work Item Comments API

**Status:** Accepted  
**Date:** 2026-07-09

## Context

The @mention detection feature requires fetching comments on AzDO work items to scan for `@{displayName}` occurrences. The Azure DevOps REST endpoint for work item comments (`_apis/wit/workItems/{id}/comments`) has existed since API version 5.1 but has never been promoted to stable GA — it remains at `7.1-preview.3`. Microsoft's own .NET SDK marks this endpoint `[Preview API]`. There is no GA alternative.

## Decision

Call the endpoint with `api-version=7.1-preview.3`, accepting the preview status under these mitigations:

1. **Best-effort only**: each work item's comment fetch has its own `try/catch`; a 404/400/403 for one item skips that item silently. The entire mentions block in `WorkItemsViewModel.RefreshAsync` is also `try/catch`-wrapped — if comments are unavailable the Mentions section is hidden, not an error state.
2. **Single constant**: the version string lives in `AzureDevOpsService.COMMENTS_API_VERSION` — one change point if/when it is promoted to GA.
3. **Deprecation window**: Microsoft guarantees a 12-week notice before deactivating any preview API version, giving adequate time to update.

## Consequences

- @mention notifications work today without any workaround.
- If Microsoft promotes the endpoint to GA, update `COMMENTS_API_VERSION` in `AzureDevOpsService.cs` and re-test.
- If Microsoft removes the endpoint without a GA replacement (unlikely), the Mentions feature degrades silently — it is best-effort, never a blocking dependency.
