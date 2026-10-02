# ADR-0008: Render untrusted Markdown with Markdig into native controls, not a browser

**Status:** Accepted  
**Date:** 2026-10-03

## Context

Work item content and comments are to be displayed and checked inside the app. That text comes from other people, so it is untrusted, and a page can show many items at once, so rendering must be cheap.

Options considered:

- **Markdown → HTML → the email WebView** (ADR-0007): faithful, but a native browser per item is heavy, has the airspace problem, and needs the same CSP/navigation hardening for every use.
- **A third-party Avalonia Markdown control** (e.g. Markdown.Avalonia): convenient, but its HTML, image-loading and link behaviour are its own and would have to be audited and pinned; Avalonia 12 support was unclear.
- **Markdig + an in-house renderer to native Avalonia controls**: Markdig is the standard, fast CommonMark parser for .NET (MIT); the renderer is small, and every isolation rule is ours and testable.

## Decision

`UI/Controls/Markdown/MarkdownView` (a `Border` with `Markdown` and `LinkCommand` properties) renders through `MarkdownRenderer`: one Markdig parse (`DisableHtml`, pipe tables, task lists, emphasis extras, autolinks), one pass over the AST into `SelectableTextBlock` inlines, `Grid` lists/tables and `Border` code/quote blocks. Isolation rules:

- **Raw HTML is never interpreted** — it is shown as literal text.
- **No network** — images are never fetched; their alt text is shown.
- **Links are inert unless http/https/mailto**, and then only execute `LinkCommand` with the `Uri`; the control never navigates by itself.
- **Bounded work** — input capped at 200 000 characters (truncation notice), blocks nested deeper than 16 flattened to text, and input Markdig itself refuses (its nesting limit throws) shown as plain text instead of failing.

## Consequences

- Rendering is fast and native: selectable text, theme brushes (`markdown*` classes in `AppStyles.axaml`), no browser process.
- Every isolation rule has a unit test; the renderer builds controls without a running application, so tests need no headless platform.
- Feature coverage is deliberate, not complete: no syntax highlighting, no footnotes, no HTML. Adding a Markdig extension means adding its rendering and tests.
- Azure DevOps stores most rich fields as **HTML**; those must be converted (to Markdown or text) before reaching this control, which would otherwise show the tags.
