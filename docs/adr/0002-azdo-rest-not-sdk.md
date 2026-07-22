# ADR-0002: Use raw HttpClient REST for Azure DevOps, not the TFS Client SDK

**Status:** Accepted  
**Date:** 2026-06-22

## Context

The original ARCHITECTURE.md specified `Microsoft.TeamFoundationServer.Client` / `Microsoft.VisualStudio.Services.Client` for Azure DevOps integration. When this package was evaluated during Phase 2, it introduced two transitive dependencies that broke the build under the project's zero-warning policy:

- `Microsoft.Data.SqlClient` — unnecessary for a desktop app that uses its own SQLite; adds ~15 MB and a wide attack surface
- `System.Security.Cryptography.Xml 5.0.0` — flagged by advisory GHSA-vh55-786g-wjwj (XML signature vulnerability); `TreatWarningsAsErrors=true` promotes the advisory to a hard build error

## Decision

Implement all Azure DevOps calls as raw HTTP requests using a typed `HttpClient` (`services.AddHttpClient<IAzureDevOpsService, AzureDevOpsService>()`). JSON is deserialized with a source-generated `JsonSerializerContext` (`AzureDevOpsJson`) to satisfy the IL2026/IL3050 analyzer requirements that would fire on reflection-based `JsonSerializer` calls.

## Consequences

- No SDK bloat or CVE transitive dependency.
- Narrower API surface — only the REST endpoints actually consumed are implemented, keeping the code small.
- More unit-testable: `HttpClient` is mockable via `HttpMessageHandler`; the SDK is not.
- Maintenance trade-off: REST endpoint paths and `api-version` values must be managed manually (see `AzureDevOpsService.cs` constants) rather than being abstracted by the SDK, but these are stable and versioned by Microsoft.
