# LazyChromeExtension — Current Specification

## Authority and status
ProjectID: LazyChromeExtension.
Source: C:\LazyAIDeckProjects\LazyChromeExtension.
Project Data is Controller-owned and is not another Source root.

The established baseline is **V0-M001-R002 — session-window-binding**, accepted
by Chat with its build, real-browser integration, and Windows GUI evidence.
R001 was the initial scaffold; R002 establishes the real session/window flow below.
M001 is Complete, M002 is In Progress, and M003 is Planned. PLAN.md / Revisions.md
remain at R002 while later implementation candidates await acceptance.

PROJECT.md owns stable project facts; PLANS.md owns the three-milestone roadmap;
PLAN.md / Revisions.md own established revision authority. CHANGELOG.md is a
transient implementation/test handoff, not acceptance authority.

## R002 components and runtime
- Windows .NET 10 WinForms caller plus Microsoft.AspNetCore.App shared framework
  for the built-in Kestrel loopback HTTP server. No third-party NuGet packages.
- Chrome 120+ and a Manifest V3 extension, package version 0.0.2 (not a Project VMR).
- Production uses normal installed Chrome with the extension enabled.
- Ordinary Chrome windows contain the requested WebApp. App-mode chrome, Native
  Messaging registration, administrator privileges, and fixed ports are not required.

## Architecture and launch flow
CallerHarness owns an ephemeral IPv4 loopback HTTP listener on an OS-assigned port.
Kestrel avoids a custom HTTP parser, third-party transport packages, a fixed port,
and system registration. Chrome APIs identify the actual sender window.

Each Launch:
1. Validates an absolute HTTP/HTTPS launch URL without embedded credentials.
2. Creates a random appSessionId and independent 256-bit capability token. Identical
   launch URLs still produce separate sessions.
3. Starts Chrome with --new-window and a CallerHarness-owned bootstrap URL:
   http://127.0.0.1:<port>/lazy-chrome-extension/bootstrap#v=1&bridge=<id>&session=<id>&token=<capability>
4. A content script restricted to that local bootstrap path sends its own URL to
   the extension; it reads no DOM/content. The extension verifies its sender ID,
   top frame, exact path/URL, profile context, and capability format.
5. The extension obtains windowId/tabId from Chrome's sender, authenticates with
   the caller, and persists the record in chrome.storage.session BEFORE acknowledging
   binding. A browserSessionId scopes Chrome IDs to this browser session.
6. The caller accepts an idempotent binding to that exact browser/window/tab tuple.
   Another window cannot replace it; another active session cannot claim the same
   browser/window tuple.
7. The extension navigates the bootstrap tab to the authenticated launch URL.
   Recovery retries this one-time transition only if that same tab remains in the
   bound window at the exact bootstrap URL. It never overwrites navigation that
   already happened.

The capability is in a fragment, not an HTTP path/query. API calls require an
Authorization Bearer header. The server listens only on 127.0.0.1, rejects other
Host names, grants no website CORS access, and limits bodies to 4 KiB. Responses
disable caching and referrers. Extension fetches refuse redirects and time out.
Tokens are not displayed or logged in test session summaries. This local capability
scheme is not a security boundary against malicious processes under the same user.

## Session ownership and state
Each record retains appSessionId, original launchUrl, browserSessionId, windowId,
initial tabId, state, and status detail.

- Launching → Bound after authenticated extension acknowledgement.
- Launching → Failed on launch error or no acknowledgement within 30 seconds.
- Bound → Closed when the exact window closes; Closed is terminal.
- Bound → Disconnected after more than 100 seconds without contact. Ownership is
  retained, and only the same tuple can resume Bound after contact returns.

After binding, window identity is authoritative. Ordinary navigation, including
origin changes, does not redefine ownership. The initial tab is bootstrap/diagnostic
state: closing or moving it does not migrate the window binding. Closing the bound
window never reassigns its session to another window.

The caller retains at most 256 records, evicting old terminal records only when
needed; active records are not evicted. The extension also bounds retained records.
Caller shutdown stops tracking and leaves Chrome windows open; the GUI says so.
Caller restart recovery and full browser restart recovery are not implemented.

## MV3 lifecycle and close delivery
chrome.storage.session owns the records, not volatile service-worker globals.
Listeners register synchronously. Operations are serialized, and every worker start,
installation/browser startup, and 30-second alarm rehydrates persisted state.
Recovery checks the existing window IDs, refreshes caller contact, and never matches
ordinary page URLs to rediscover ownership.

Window removal persists a Closed outbox entry before reporting to the caller.
A subsequent worker/alarm retries delivery after transient loopback failure.
Recovery also discovers a window whose removal event was missed. Acknowledged
closed entries are removed; closed entries with failed delivery expire after five
minutes. Active records remain tied to their existing windows. An explicit caller
rejection removes an obsolete extension entry.

Session storage survives worker termination but clears on browser restart or
extension reload/update/disable; those are outside the R002 survival guarantee.
The caller reports loss of contact rather than asserting survival across restart.

## Permissions and exclusions
Permissions: storage, alarms, and host access to http://127.0.0.1/* for the variable
port. Chrome match patterns cannot restrict the port; the handler additionally
requires the exact bootstrap path and authenticated caller identity. Content scripts
match only that path. There is no tabs permission, broad remote host access, native
messaging, debugger permission, or speculative capture permission. Basic window/tab
lifecycle and navigation APIs are used without reading remote page content.

R002 implements no geometry, PARK, SetWindowPos, HWND lookup, capture, preview,
monitor, DOM scraping, OCR, semantic analysis, ChatGPT selectors/output extraction,
response interception, or page-content automation.
M002 — Geometry / Park / Restore is In Progress. M003 — Human monitor + integrated
V0 acceptance remains Planned.

## Caller GUI and configuration
The GUI provides Launch URL (default https://chatgpt.com/), Launch, status detail,
and a table of session IDs, window IDs, states, and original launch URLs.
Chrome process launch and listener operations do not block the UI thread.

With an enabled extension in normal Chrome, no arguments are needed. Controlled
acceptance can use --chrome-executable <chrome.exe> and
--chrome-user-data-dir <directory>. The executable must be an existing Chrome
product executable; arbitrary process flags are not accepted. CallerHarness adds
no debugging or extension-loading flags. Tests prelaunch Chrome for Testing with
automation flags using the same canonical isolated profile path. Product Source
contains no developer-specific executable/profile paths.

## Windows validation
From the Source root:

    dotnet restore .\LazyChromeExtension.sln
    dotnet build .\LazyChromeExtension.sln --no-restore --configuration Debug
    .\Scripts\Test-R002.ps1 -ChromeExecutable '<official portable Chrome for Testing chrome.exe>'

The test entry requires PowerShell 7 and Node.js 22+ built-in modules only. It runs
caller/HTTP checks, deterministic tests of the production extension rehydration
path, and Chrome integration with neutral local HTTP servers and a fresh TEMP
profile. A separate test assembly invokes the same SessionHost.LaunchAsync used by
the GUI; there is no product test backdoor.

Integration covers one session, identical-URL concurrent sessions, completed
cross-origin navigation, window close, and CDP worker stop followed by the production
alarm's restart. Windows Computer Use separately validates the actual WinForms
Launch flow. See CHANGELOG.md for actual outcomes and evidence.

Logs: Scripts/Outputs/<timestamp>-Test-R002.log. Profiles may remain under TEMP
after normal process shutdown. No user profile is used during automated validation.

## Chrome API references
- https://developer.chrome.com/docs/extensions/reference/api/storage
- https://developer.chrome.com/docs/extensions/reference/api/windows
- https://developer.chrome.com/docs/extensions/reference/api/alarms
- https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle
