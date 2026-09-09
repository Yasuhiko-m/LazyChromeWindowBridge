# LazyChromeExtension — Current Specification

## Authority and status
ProjectID: LazyChromeExtension.
Source: C:\LazyAIDeckProjects\LazyChromeExtension.
Project Data is Controller-owned and is not another Source root.

The established baseline is **V0-M003-R004 — human-monitor-integration**, accepted
by Chat with its build, real-browser/native integration, and Windows GUI evidence.
R001 was the scaffold; R002 established session/window binding; R003 established
the geometry/native additions specified below; R004 established the human preview.
M001, M002 and M003 are Complete. M004 runtime tuning is In Progress; M005 remains
roadmap only. PLAN.md / Revisions.md remain at R004 during subsequent candidate work.

PROJECT.md owns stable project facts; PLANS.md owns the three-milestone roadmap;
PLAN.md / Revisions.md own established revision authority. CHANGELOG.md is a
transient implementation/test handoff, not acceptance authority.

## R002 components and runtime
- Windows .NET 10 WinForms caller plus Microsoft.AspNetCore.App shared framework
  for the built-in Kestrel loopback HTTP server. No third-party NuGet packages.
- Chrome 120+ and a Manifest V3 extension, candidate package version 0.0.4 (not a Project VMR).
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
Host names, grants no website CORS access, and limits HTTP bodies to 4 KiB. The R004
WebSocket has separately bounded authentication and frame messages below. Responses
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
Permissions: storage, alarms, debugger, and host access to http://127.0.0.1/* for the variable
port. Chrome match patterns cannot restrict the port; the handler additionally
requires the exact bootstrap path and authenticated caller identity. Content scripts
match only that path. There is no tabs permission, broad remote host access, native
messaging, or speculative capture permission. R004 uses debugger only for implemented
pixel capture and viewport geometry. Basic window/tab
lifecycle and navigation APIs are used without reading remote page content.

The established R002 checkpoint contains no geometry or native placement. R003 adds
geometry/PARK/RESTORE only. Capture and preview first appear in the R004 candidate.
No product revision implements DOM scraping, OCR, semantic analysis, ChatGPT
selectors/output extraction, response interception, or page-content automation.
Desktop monitor enumeration is placement topology, distinct from visual monitoring.
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
    .\Scripts\Test-R004.ps1 -ChromeExecutable '<official portable Chrome for Testing chrome.exe>'

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

## R004 candidate: human monitor architecture
One selected session per CallerHarness has a live PictureBox preview, with Start
monitor / Stop monitor controls. The target label includes the window ID and short
session ID; the table includes full session IDs and Visible / Parked / Closed. The monitor label reports
state, dimensions, frame count, age, and errors. Selecting another bound row while
monitoring immediately clears the previous image and starts the new target.
PARK/RESTORE never substitutes a different window or changes the monitor selection.

The extension's MonitorManager uses the established appSessionId, browserSessionId
and windowId record. It queries only the active tab of that exact window, attaches
chrome.debugger to its tabId, and issues only Page.getLayoutMetrics (viewport
geometry) and Page.captureScreenshot (JPEG pixels). After capture it verifies the
tab still belongs to that window and is active; a moved tab's frame is discarded.
Active-tab changes detach the old target and acquire the new active tab in the same
owned window. URL/title matching is never a monitor identity mechanism. The caller
requires the same authenticated session, windowId, native HWND/PID/property identity,
connection ID, and current selection generation before and after image decoding.

Frames travel through a capability-authenticated WebSocket on the existing loopback
listener at /lazy-chrome-extension/monitor. The first message contains session ID
and token, is bounded to 4 KiB, and must arrive within three seconds. The token is
not in the WebSocket URL or logs. Only one live connection per session is accepted.
Frame messages are bounded to 3 MB, with at most 2.8 million Base64 characters; JPEG
metadata and dimensions are validated. Control messages every 500 ms select target,
generation, settings, stop, and shutdown. Only the local caller UI controls capture;
there is no remote control HTTP endpoint. Error/closed states clear the preview.

Default: **2 fps, maximum 960×540, JPEG quality 70**, aspect preserving and without
upscaling. Supported internal settings are 1–10 fps and maximum output up to
1920×1080. The GUI deliberately exposes the chosen default only. Chrome captures
the page viewport; browser tabs, address bar, permission banner, desktop, other
windows, and off-viewport content are not included. Device-scale oversize output
is bounded again by caller resizing. At most one acquisition per target is in
flight. A socket backlog of 1 MB skips sending the current frame; the caller keeps
only the latest frame, disposing replaced GUI images. There is no recording archive.

Pixel transport, JPEG decoding/resizing, and display are the entire product image
path. No OCR, DOM, Runtime evaluation, network-response inspection, classifiers,
ChatGPT selectors, completion detection, output extraction, semantic inspection,
or automated decisions based on imagery are implemented. Deterministic neutral-page
pixel hashes/color checks exist solely in test tooling to prove freshness/isolation.

## R004 capture selection and permissions
Two approaches were actually tried on this Windows host. Native PrintWindow with
PW_RENDERFULLCONTENT produced changing visible frames but repeated the identical
last frame while fully PARKED, so that prototype was rejected and removed. Chrome's
supported debugger screenshot API produced fresh frames both visibly and fully
offscreen, without moving/restoring the native window during capture. Windows
Graphics Capture and tabCapture were considered, not implemented or empirically
tested. Only the adopted Chrome capture stack remains in the product.

The extension now declares the broad **debugger permission**. Installing/updating
it requires accepting Chrome's normal permission grant. Attaching invokes Chrome's
debugging notice with its normal user cancellation behavior. There is no additional
per-session permission picker, required Chrome-action gesture, Windows capture
picker, DevTools manipulation, or recurring consent dialog in this API workflow.
The operator starts monitoring in CallerHarness. No banner suppression, sandbox
bypass, experimental security flag, administrator right, or cloud relay is used.
The broader permission capability is a real tradeoff even though product commands
are limited to pixels/geometry. Chrome enterprise screenshot/debugger restrictions
can prevent capture; ordinary policy must permit the extension and screenshots.
DevTools contention, protected targets/content, cancellation, or capture errors are
reported as monitor errors. The operator must explicitly press Start monitor to
retry; the same failed generation never repeatedly reattaches.

Permission behavior is the supported Chrome API contract; deterministic acceptance
used an unpacked extension in a fresh Chrome for Testing profile. It did not validate
normal Chrome's installation consent UI or an enterprise-managed policy deployment.
The optional authenticated ChatGPT/normal-profile smoke was skipped; no account,
normal-profile content, or ChatGPT page was automated. Neutral local pages are the
acceptance authority.

## R004 lifecycle and failure containment
Stop, selection changes, window close, and lost caller transport detach the captured
tab and discard late frames. Start/Stop are idempotent; retry or target changes use
a new generation. User debugger cancellation also discards an in-flight frame.
PARK/RESTORE and ordinary navigation preserve binding, native identity, and the
original launch profile. Capture errors do not modify session or geometry state.

Chrome 116+ WebSocket activity resets worker idle time; Chrome 118+ active debugger
sessions keep the worker alive. The existing Chrome 120 minimum covers both.
Worker recreation cleans only target IDs persisted by this extension, rehydrates
the existing session records, reconnects to the caller, and resumes the selected
generation. Forced real worker termination followed by the existing alarm recovery
is tested. Ordinary MV3 idle suspension is prevented while the monitor transport is
active, rather than being treated as a reason to lose binding.

Normal caller shutdown sends stop/closing control and allows up to three seconds
for streams to close before stopping the listener, then performs R003 native cleanup.
Capture has a five-second timeout; transport loss also triggers extension detach.
There is no capture subprocess, offscreen document, native capture handle, or new
background service. Actual normal-shutdown acceptance verifies zero monitor
connections/capturing connections and detached owned Chrome targets before browser
test cleanup. A hung/forcibly killed caller cannot guarantee synchronous detach;
transport closure and extension cleanup are the fallback. Full browser/caller
restart, extension reload, locked desktop, minimized windows, protected media, and
all hardware/GPU combinations are not acceptance guarantees.

## R004 empirical acceptance and performance
Windows / Chrome for Testing 153.0.8010.36, three 1920×1080 monitors at x=-1920,
0,1920, all 96 DPI. Real negative-coordinate hardware is covered by retained R003
acceptance. Mixed DPI and monitor removal retain deterministic simulated coverage,
not additional physical evidence.

The real integrated flow saves and relaunches geometry, binds A/B independently,
captures changing local A, PARKs A at (0,-864,1280,800), confirms zero intersection
with every native monitor, observes changing frames, navigates across local origins
while PARKED, RESTOREs the identical HWND to (40,50,1280,800) with 0-pixel error,
switches to blue B, and validates close and normal caller shutdown cleanup. Neutral
fixtures change every 250 ms. Multiple distinct center-frame hashes while PARKED
prove freshness rather than retransmission of a frozen last frame.

One complete production run measured the following four-second windows. CPU is
process CPU time / elapsed time, expressed as percent of one core; Chrome totals
include the owned test browser process tree. Values are approximate and include
test-driver hashing/sampling overhead, not isolated rendering cost. The default's
actual viewport output was 960×493; the larger setting produced 1264×649, preserving
aspect and avoiding upscaling. Bytes/s count Base64 frame payload, excluding small
JSON/WebSocket headers. Memory is working set / private bytes in MiB.

| Requested fps / maximum | State | Effective fps | Payload B/s | Caller CPU % | Chrome CPU % | Caller MiB | Chrome MiB |
|---|---|---:|---:|---:|---:|---:|---:|
| 1 / 960×540 | Visible | 0.98 | 9,110 | 3.45 | 5.37 | 73.0 / 24.7 | 796.0 / 462.1 |
| 1 / 960×540 | Parked | 0.98 | 9,166 | 3.07 | 4.99 | 73.9 / 27.3 | 795.4 / 460.8 |
| 2 / 960×540 | Visible | 1.99 | 18,734 | 1.94 | 3.88 | 73.6 / 25.2 | 795.2 / 458.6 |
| 2 / 960×540 | Parked | 1.98 | 18,600 | 3.09 | 11.19 | 72.6 / 24.2 | 795.4 / 458.5 |
| 5 / 1280×720 | Visible | 4.87 | 74,440 | 6.85 | 13.32 | 72.7 / 24.3 | 798.6 / 466.4 |
| 5 / 1280×720 | Parked | 5.22 | 79,791 | 5.44 | 15.54 | 73.9 / 26.2 | 792.2 / 456.5 |
| 10 / 1280×720 | Visible | 9.47 | 144,714 | 9.74 | 20.64 | 73.1 / 24.6 | 790.6 / 453.3 |
| 10 / 1280×720 | Parked | 10.01 | 153,344 | 9.53 | 23.26 | 72.7 / 24.2 | 790.6 / 453.6 |

Source log: Scripts/Outputs/20260909-141510086-Test-R004.log. Final regression logs
and exact frame/identity samples are identified in the transient R004 handoff.
Short windows include frame-boundary variation, hence slightly over nominal rates.
At 5/10 fps some frames legitimately repeat the 4-Hz fixture content; the test's
90-ms sampler can miss sequences. No sustained frozen PARK stream was observed.
Socket backlog drops were not forced or directly counted. PARK did not stop fresh
capture; these short runs are not a long-duration memory-leak or worst-case video
bandwidth benchmark. The 2-fps default is appropriate for a human status preview,
with substantially lower transfer and load than 10 fps in this reference fixture.

Test-R004.ps1 runs all caller checks, binding/monitor extension tests, the unchanged
R002 browser scenarios, R003 geometry scenarios, and production R004 integration.
Its -Gui mode supplies isolated dynamic local pages for actual WinForms acceptance.
GUI evidence exercises selection, Start/Stop, PARK/RESTORE, changing previews, and
closing the caller without requiring developer tooling in the product workflow.

References:
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow
- https://developer.chrome.com/docs/extensions/reference/api/debugger
- https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-captureScreenshot
- https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-getLayoutMetrics
