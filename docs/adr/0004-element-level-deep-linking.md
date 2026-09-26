# ADR-0004: Element-level deep-linking via NavigationTarget + IDeepLinkTarget, not a generic event bus

**Status:** Accepted
**Date:** 2026-09-25

## Context

The pre-rewrite app's notifications carried a bare `Action? onActivated` closure. At best this navigated
to a whole page (`NavigateTo(typeof(EmailViewModel))`); there was no way to land on the specific email,
pull request, or work-item-mention row that triggered the notification. The user explicitly asked for
this as a first-class capability of the rewrite: "the notification can target something in the UI and
switch to a page in the UI to a specific element."

Two designs were considered:

1. **A generic event bus / mediator** that a notification publishes to, with pages subscribing to
   "focus this id" events. Rejected: it decouples the sender from the receiver at the cost of making the
   actual navigation-then-focus sequencing implicit and harder to test — you'd need to prove a subscriber
   was actually listening at the right time, across an async page-load race, with no compile-time link
   between a notification and what it's allowed to target.
2. **A structured navigation target plus an opt-in per-page interface**, giving the navigation service
   itself the responsibility of "arrive, then, if asked, focus" in one traceable call. Chosen.

## Decision

- `NavigationTarget(Type ViewModelType, string? ElementId = null, object? Parameter = null)` is the only
  vocabulary a notification (or anything else) uses to describe "go here, and if you can, focus this."
- `INavigationService.NavigateTo(NavigationTarget)` is the only navigation entry point. After resolving
  and activating the target page, if `ElementId` is set and the page implements `IDeepLinkTarget.FocusElement(string)`,
  it's called.
- A page that wants to be a deep-link target implements `IDeepLinkTarget`, exposes a `TargetFor(id)`
  static helper so callers never hand-format element ids, and defers a focus request that arrives before
  its data has loaded (first visit, or while signed out) via a pending-id field applied once loading
  finishes — see `TodoViewModel` (the reference implementation) and `WorkItemsViewModel` (which
  distinguishes two target kinds on one page via an `"item:{id}"`/`"mention:{id}"` prefix scheme).
- The actual scroll-and-highlight is a single shared, feature-agnostic Avalonia attached behavior,
  `ScrollIntoViewBehavior`, bound to a `ListBox` and a `HighlightedItem`-shaped property. It resets its own
  bound property after firing, so a repeated notification for the same row still flashes.
- `NotificationEntry.Target` is a `NavigationTarget?` instead of a bare closure; `Activate()` calls
  `INavigationService.NavigateTo(Target)`.

## Consequences

- A notification's capability is data (a `NavigationTarget`), not code — it's trivially testable
  end-to-end (build the real `AddUi()` composition, fire `NotificationCenterViewModel.Add(...)`, execute
  `ActivateCommand`, assert the landed page's highlighted item) without any fake event-bus plumbing. Every
  feature phase that added deep-linking (Todo, Email, PullRequests, WorkItems) has exactly this test shape.
- Any list-bearing feature that wants deep-linking must switch from a bare `ItemsControl` to a real
  `ListBox` with a `SelectedItem`/highlight property — this was already a known gap (`EmailView`/
  `WorkItemsView` used plain `ItemsControl`s, which have no selection concept at all) and fixing it was a
  deliberate part of adopting this pattern, not an accident.
- Features with no notification-driven use case (Calendar, Teams, Automations, Dashboard) simply don't
  implement `IDeepLinkTarget` — the mechanism is opt-in per page, not a tax every feature pays.
- The `Parameter` field on `NavigationTarget` exists for future use (e.g. passing structured filter state)
  but nothing consumes it yet as of this ADR.
