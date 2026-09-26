# ADR-0003: Vertical feature-slice modules discovered by reflection, not flat registration files

**Status:** Accepted
**Date:** 2026-09-25

## Context

The pre-rewrite app registered every section by hand in a fixed set of shared files: a `ViewLocator` that
guessed a View's type name by string-replacing `ViewModel` → `View` on the ViewModel's full name, a flat
`AddTransient<XxxViewModel>()` list per page in `PresentationServiceCollectionExtensions`, a matching list
in `InfrastructureServiceCollectionExtensions`, and a hardcoded `NavigationItem[]` array literal in
`MainWindowViewModel` for the sidebar. Adding or changing a section meant touching all of these files by
hand, in the right order, with no compiler check that any of them agreed with each other. Missing one
step failed silently: `ViewLocator` rendered a "Not Found:" placeholder instead of throwing, and a missed
DI registration only surfaced the first time a user actually clicked into that page.

This was the direct motivation for the rewrite: the user reported that adding a section reliably produced
silent breakage scattered across files that had no structural relationship to each other.

Alternatives considered:
- **Source generator** emitting the registration code at compile time. Rejected for this codebase's
  scale — reflection-based discovery is simpler to reason about and fast enough for a single-user desktop
  app with a handful of features; a source generator's build-time complexity wasn't worth it here.
- **A DI-container convention scanner** (e.g. Scrutor-style `AddClassesAssignableTo`). Considered, but a
  bespoke `ModuleDiscovery.Find<T>` was small enough to own directly and let each layer discover a
  layer-specific module contract (`IInfrastructureModule`/`IPresentationModule`/`IViewModule`) rather than
  scanning for services generically.

## Decision

Organize every feature as **one folder per project layer**, `Features/<Name>/`, and have it register
itself through a small module contract discovered by reflection at composition time:

- `IInfrastructureModule.RegisterServices(IServiceCollection, IConfiguration)` — Infrastructure layer.
- `IPresentationModule { NavigationItem? MenuEntry; RegisterServices(IServiceCollection) }` — Presentation
  layer; the sidebar is built purely from discovered `MenuEntry`s, sorted by `Order`.
- `IViewModule { IReadOnlyDictionary<Type,Type> ViewModelToView }` — UI layer; a `ViewRegistry` merges
  every module's map and `ViewLocator` looks views up in it instead of guessing by name.
- `IAutomationModule { AutomationDescriptor; Type JobType }` — Automation layer, for Quartz jobs.

`ModuleDiscovery.Find<T>(assemblies)` returns one instance of every non-abstract, public
parameterless-constructible `T` in the scanned assemblies. Each layer's composition method
(`AddInfrastructure()`, `AddUi()`, `AddAutomations()`) calls it once, scanning only its own layer's
assembly by default.

Critically, `CompositionRoot.Build()` runs a `ShellCompositionValidator` immediately after building the
container: it resolves every registered page ViewModel and confirms each has a `ViewModelToView` entry
whose View type is constructible, throwing immediately — naming the offending type — if anything is
missing. This converts what used to be a silent runtime "Not Found" or a deferred DI failure into a
startup-time crash with an actionable message.

## Consequences

- Adding a feature no longer means editing any shared file (`PresentationServiceCollectionExtensions`,
  `InfrastructureServiceCollectionExtensions`, `MainWindowViewModel`, `ViewLocator`) — dropping in a
  `<Name>InfrastructureModule`/`<Name>PresentationModule`/`<Name>ViewModule` in the right `Features/<Name>/`
  folder is sufficient. The Todo slice (`Features/Todo/` across all four layers) is the copy-paste
  reference template; see CLAUDE.md's "Recipe: adding a new feature."
- A broken or missing View mapping now fails loudly at app launch instead of silently at click-time,
  which was the specific pain point that motivated this ADR.
- Module classes must be `public sealed` with a public parameterless constructor — reflection discovery
  can't call anything else. This is a minor constraint on an otherwise plain DI-registration class.
- Reflection-based discovery means these entry points (`ModuleDiscovery.Find`, `AddInfrastructure`,
  `AddUi`, `AddAutomations`) carry `[RequiresUnreferencedCode]` and are exempt from full AOT compatibility;
  this is consistent with the rest of the app already being non-AOT (EF Core, Graph, MSAL, Playwright,
  Quartz are all reflection-based too — see the AOT note in CLAUDE.md).
