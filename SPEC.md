# LazyChromeExtension — Current Specification

## Authority and status
ProjectID: LazyChromeExtension.
Source: C:\LazyAIDeckProjects\LazyChromeExtension.
Project Data is Controller-owned and is not another Source root.

The established baseline is **V0-M002-R003 — geometry-park-restore**, accepted
by Chat with its build, real-browser/native integration, and Windows GUI evidence.
R001 was the scaffold; R002 established session/window binding; R003 established
the geometry/native additions specified below. M001 and M002 are Complete and
M003 is In Progress. PLAN.md / Revisions.md remain at R003 while later candidates
await acceptance.

PROJECT.md owns stable project facts; PLANS.md owns the three-milestone roadmap;
PLAN.md / Revisions.md own established revision authority. CHANGELOG.md is a
transient implementation/test handoff, not acceptance authority.

## R002 components and runtime
- Windows .NET 10 WinForms caller plus Microsoft.AspNetCore.App shared framework
  for the built-in Kestrel loopback HTTP server. No third-party NuGet packages.
- Chrome 120+ and a Manifest V3 extension, package version 0.0.3 (not a Project VMR).
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
   http://127.0.0.1:<port>/lazy-chrome-extension/bootstrap?session=<id>#v=1&bridge=<id>&session=<id>&token=<capability>
4. A content script restricted to that local bootstrap path sends its own URL to
   the extension; it reads no DOM/content. The extension verifies its sender ID,
   top frame, exact path/URL, profile context, and capability format.
5. The extension obtains windowId/tabId from Chrome's sender, authenticates with
   the caller, and persists the record in chrome.storage.session BEFORE acknowledging
   binding. A browserSessionId scopes Chrome IDs to this browser session.
6. The caller accepts an idempotent binding to that exact browser/window/tab tuple.
   Another window cannot replace it; another active session cannot claim the same
   browser/window tuple.
7. R003 obtains the native mapping acknowledgement described below before the
   extension navigates the bootstrap tab to the authenticated launch URL.
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
Caller shutdown attempts to restore parked native windows, stops tracking, and leaves Chrome open.
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

The established R002 checkpoint contains no geometry or native placement. R003 adds
geometry/PARK/RESTORE only. Neither implements capture, preview, human monitoring,
DOM scraping, OCR, semantic analysis, ChatGPT selectors/output extraction,
response interception, or page-content automation. Desktop monitor enumeration is
placement topology, not M003 visual monitoring.
M002 — Geometry / Park / Restore is Complete. M003 — Human monitor + integrated
V0 acceptance is In Progress.

## Caller GUI and configuration
The GUI provides Launch URL (default https://chatgpt.com/), Launch, status detail,
and a table of session IDs, window IDs, states, and original launch URLs. R003 adds
Park selected / Restore selected buttons and a target window/session label. The
selected row, never the edited launch URL, determines the operation target.
The table displays Bound / Mapping, Bound / Visible, Bound / Parked, or Closed;
operation failures remain in status/detail. Duplicate operations are idempotent in
the coordinator; the UI disables an operation already satisfied by current state.
Chrome process launch and listener operations do not block the UI thread.

With an enabled extension in normal Chrome, no arguments are needed. Controlled
acceptance can use --chrome-executable <chrome.exe> and
--chrome-user-data-dir <directory>. Geometry persistence can be isolated with
--geometry-directory <directory>. The executable must be an existing Chrome
product executable; arbitrary process flags are not accepted. CallerHarness adds
no debugging or extension-loading flags. Tests prelaunch Chrome for Testing with
automation flags using the same canonical isolated profile path. Product Source
contains no developer-specific executable/profile paths.

## Windows validation
From the Source root:

    dotnet restore .\LazyChromeExtension.sln
    dotnet build .\LazyChromeExtension.sln --no-restore --configuration Debug
    .\Scripts\Test-R002.ps1 -ChromeExecutable '<official portable Chrome for Testing chrome.exe>'
    .\Scripts\Test-R003.ps1 -ChromeExecutable '<official portable Chrome for Testing chrome.exe>'

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

R003's Source-owned entry runs all R002 regressions plus geometry unit tests and
real native acceptance. It reuses the R002 fixture and invokes the same production
GeometryCoordinator and NativeWindows paths as the GUI. Test-driver commands exist
only in the separate test executable; no geometry control endpoint is exposed by
the product. R003 logs use <timestamp>-Test-R003.log. Optional -Gui starts the real
WinForms caller, two neutral local URL targets, and an isolated CfT profile for an
operator/Computer Use acceptance run; closing the caller ends that fixture.
GUI outcomes must be recorded separately; fixture startup alone is not GUI PASS.

## R003 geometry authority and profiles
CallerHarness owns normal placement and logical PARK state. GeometryStore persists
schema-version-1 JSON per normalized immutable launch URL, keyed by its UTF-8 SHA-256
filename in %LOCALAPPDATA%\LazyChromeExtension\Geometry by default. Records include
launch URL, left/top/width/height in physical pixels, observed DPI, and save time.
Same-directory temporary write followed by atomic replacement prevents partial JSON.
Missing, malformed, or incompatible records are ignored; I/O/native failures are
not successful operations. The directory is local user data, not Source or Project Data.

The existing .NET URI canonicalization normalizes scheme/host/default port and URI
syntax. Path, query, and fragment remain part of profile identity. Separate URLs
never share files. Same-URL simultaneous sessions deliberately share the profile:
the last changed normal geometry saved wins, while each active session retains its
own remembered normal rectangle for RESTORE. Existing windows are not repositioned
when another session updates their shared file. Navigation never changes this key.

A 500-ms observer accepts only a normal (not minimized/maximized), reachable visible
window rectangle that remains stable for at least 750 ms. It writes only changed
rectangles. No save happens from Parking/Parked/Restoring states. PARK synchronously
saves the current valid normal rectangle before moving. If the window is minimized
or maximized, PARK uses the last learned normal rectangle and normalizes the native
window before moving. Without a prior learned/profile rectangle, initial Chrome
placement supplies the first rectangle; maximized-state restoration is not persisted.
Caller normal shutdown also flushes valid visible placement and attempts to return
parked windows before releasing native tags. Forced caller termination cannot perform
that cleanup; session recovery after caller/browser restart remains outside scope.

## R003 exact native mapping and coordinate boundary
The authenticated R002 sender tuple remains the ownership authority. While its
bootstrap page is still displayed, a separate authenticated /native acknowledgement
maps the session to HWND. The caller renders only its own GUID marker title:
`LazyChromeExtension Session <appSessionId>`. The public query GUID must match the
fragment GUID; it is not a capability. The token stays in the fragment/header.

NativeWindows enumerates visible top-level Chrome_WidgetWin_1 windows, requires the
exact marker title (with optional standard Chrome title suffix) and configured
Chrome executable path, and accepts exactly one candidate. It never chooses first,
foreground, or a remote page URL/title. A per-HWND Windows property tag plus PID is
retained with that HWND. All later reads/moves require the same HWND/PID/tag; a stale
or reused handle is rejected rather than rediscovered. Concurrent markers cannot
select one another. Native mapping must succeed before bootstrap navigation.
Transient mapping failure returns 503; the extension retains nativePending and
retries on bootstrap/alarm/worker recovery. Once acknowledged, normal worker restart
does not remap or reapply startup geometry. There is no remote page DOM access.

Geometry APIs run in a scoped PER_MONITOR_AWARE_V2 thread context. GetWindowRect,
SetWindowPos, and EnumDisplayMonitors/GetMonitorInfo use physical screen pixels,
including Windows' invisible resize borders. Chrome DIP/window-bound values are
never mixed with these coordinates; there is no Chrome-to-Win32 scale conversion.
Persisted physical size is preserved across DPI changes. GetDpiForWindow reports
actual bound-window DPI; GetScaleFactorForMonitor reports each monitor's scale
percentage, with nominal layout DPI derived as scale * 96 / 100. These are evidence,
not a second geometry scale multiplier. Failed DPI context
or incomplete monitor enumeration prevents placement. Native moves are asynchronous
without activation/z-order changes and verify each coordinate/dimension within 2 px,
retrying boundedly for Chrome's DPI/placement processing.

## R003 PARK, RESTORE, and changing topology
States are Visible, Parking, Parked, Restoring, Closed. PARK is logical, never inferred
from coordinate signs. A monitor left/above primary can have valid negative normal
coordinates. PARK computes top = minimum active monitor top - normal height - 64,
using primary monitor left as x, then verifies the actual native rectangle intersects
none of the complete current monitor bounds. The Chrome window remains alive.
If a new monitor covers a parked rectangle, the observer deliberately reparks it.
Normal geometry is unchanged throughout PARK, navigation, and worker restarts.

RESTORE moves the same retained native identity to its session's remembered normal
rectangle. Startup similarly applies the original launch profile after exact mapping.
Reachability requires at least 100 horizontal pixels of title bar on a work area,
with 40 vertical pixels available and a 12-pixel top-border allowance. An otherwise
reachable saved position is preserved. If topology/work-area changes make it
unreachable, choose greatest work-area overlap, then primary, then ordinal device
name; clamp size and placement into that work area. This deterministic visible
fallback is saved as normal. No active monitors or a failed native move is an error,
never a successful restore. Failed transitions retain protected normal placement
and remain recoverable through Restore; they never learn a partial/offscreen move.

Closing a parked window becomes Closed, retaining the last normal profile. Native
destruction invalidates its tag; later commands cannot target a replacement handle.
Acceptance records actual hardware rectangles/DPI separately from deterministic
negative/above-primary, mixed-DPI, changed-work-area, and monitor-removal simulations.

## Windows API references
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext
- https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getscalefactorformonitor

## Chrome API references
- https://developer.chrome.com/docs/extensions/reference/api/storage
- https://developer.chrome.com/docs/extensions/reference/api/windows
- https://developer.chrome.com/docs/extensions/reference/api/alarms
- https://developer.chrome.com/docs/extensions/develop/concepts/service-workers/lifecycle
