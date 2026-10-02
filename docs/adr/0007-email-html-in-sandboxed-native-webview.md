# ADR-0007: Render email HTML in a sandboxed native WebView

**Status:** Accepted  
**Date:** 2026-10-03

## Context

The Email reading pane showed the plain-text body Graph produces on request (`Prefer: outlook.body-content-type="text"`). Outlook's HTML-to-text conversion turns every link into `text <url>` — Safe Links URLs run to hundreds of characters — and every image into `[url]`, so automated mail (Azure DevOps notifications, security alerts) was unreadable. Cleaning the text helps, but layout, tables, colours and inline images are lost: the page needed to display mail the way an email client does.

Email HTML is untrusted, attacker-controlled input. Rendering it must not run script, leave the app, or reveal to the sender that the message was opened (remote images / tracking pixels).

Options considered:

- **A managed HTML renderer** (HtmlRenderer-style, CSS 2): no native dependency and no script engine at all, but Outlook/newsletter layouts render poorly and Avalonia 12 support was unverified.
- **`Avalonia.Controls.WebView` (`NativeWebView`)**, MIT, versioned with Avalonia (12.1.0): the platform engine — WebView2 on Windows, WebKitGTK on Linux, WKWebView on macOS — so mail renders faithfully. It ships a script engine, so it must be locked down.

## Decision

Use `NativeWebView`, sandboxed in four layers:

1. **Content-Security-Policy first.** `EmailHtmlDocument.Build` prepends `<meta http-equiv="Content-Security-Policy">` before any of the sender's markup: `default-src 'none'; style-src 'unsafe-inline'; font-src data:; img-src data:` (plus `https: http:` only after the user clicks *Load images* for that message), `form-action 'none'; base-uri 'none'`. No `script-src`, so inline scripts and event handlers never run; a policy the sender adds can only tighten it. `<meta http-equiv=refresh>` is stripped.
2. **Inline images without the network.** `GetEmailHtmlAsync` replaces `cid:` references with `data:` URIs from the message's inline attachments (images only), so signatures and screenshots show while every remote fetch stays blocked.
3. **Every navigation intercepted** (`ReaderNavigationGuard`). Only the document the view just handed over may load — the platform reports it as `about:blank` (WebKitGTK) or a `data:` URL (WebView2); any later `data:` navigation is blocked because it would load a document *without* the CSP. `about:` anchors stay in-page; http/https/mailto are cancelled and handed to `IBrowserLauncher`; every other scheme is dropped. New-window requests are handled the same way.
4. **Ephemeral environment.** In-private WebView2 profile, ephemeral WebKitGTK data manager, non-persistent WKWebView store: nothing a message does survives the session.

The plain-text body (cleaned by `EmailTextCleaner`) is kept for the AI digest, which must not receive markup.

## Consequences

- Mail renders like Outlook; links work through the user's browser; tracking pixels stay blocked by default.
- A native dependency per OS: WebView2 runtime on Windows (preinstalled on Windows 11), `webkit2gtk-4.1` on Linux.
- WebKitGTK paints an empty pane with accelerated compositing on NVIDIA under Wayland/XWayland. `Program.ConfigureWebKit` sets `WEBKIT_DISABLE_COMPOSITING_MODE=1` through libc `setenv` — `Environment.SetEnvironmentVariable` only changes .NET's managed copy on Unix, which WebKit (`getenv`) and its web process never see.
- The WebView is a native window drawn above Avalonia content (airspace): overlays such as toasts can be hidden behind the reading pane.
- The navigation policy lives in a plain class (`ReaderNavigationGuard`) so it is unit-tested; the CSP builder is pure and tested too. The WebView itself is not exercised by tests.
