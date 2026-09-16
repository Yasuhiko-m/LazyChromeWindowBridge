# LazyChromeWindowBridge — Current Specification

ProjectID: LazyChromeExtension.
Source: C:\LazyAIDeckProjects\LazyChromeExtension.
Project Data is Controller-owned and is not another implementation root.

## Authority
Established baseline: **V1-M011-R021 — sdk-servicing-line-v031**, accepted by Chat.
Revisions.md establishes Current VMR; PLAN.md mirrors it. V1 M001–M011 and V0 are
Complete; V1 remains In Progress. Source version 0.3.1 is accepted. M009 establishes
default offscreen rendering preservation and bounded caller launch switches; M010
establishes capture-region/resize/source-size plus preview-liveness stabilization; M011
establishes release-ready distribution documentation and package consistency. R019 restores
only the Controller validation map. R021 adopts the stable .NET 10.0.4xx toolchain policy:
10.0.401 floor, latestPatch roll-forward and no prerelease SDK. Neither alters the accepted
0.3.1 specification. Controller validate-revision, Git checkpoint, push and publication
remain separate boundaries.

R013 preserves the accepted ownership, geometry, PARK/RESTORE, caller-owned monitoring
and download contracts while adding source-compatible `CaptureMode.BrowserViewport` /
`CaptureMode.NativeWindow` selection and independent exact-HWND taskbar visibility.
BrowserViewport remains the default two-command CDP path. NativeWindow uses exact-HWND
Windows Graphics Capture plus D3D11 bounded resize and no picker/CDP screenshot fallback.
`SetShowInTaskbar` is runtime-only and restored on normal disposal when LCWB changed it.

Historical GitHub/NuGet 0.1.0 and 0.2.0 releases and the submitted CWS 0.2.0 review
remain unchanged. R014 accepts 0.3.0 distribution preparation and the user has separately
authorized GitHub v0.3.0 and NuGet 0.3.0 publication after the accepted checkpoint;
no Chrome Web Store operation is part of R014.

Historical V1 M002/R008 established MIT licensing, public documentation, a neutral
demo screenshot and deterministic extension packaging without changing R007 runtime.
Those historical assets do not demonstrate the newer accepted monitoring behavior.

R006 preserves the externally observable R005 contract. The prior accepted history
and measurements remain in Revisions.md and the R005 Git checkpoint. Current
maintainable product documentation is in README.md and docs/.

## Components and API
- LazyChromeWindowBridge.Core: dual-target net10.0-windows reusable library, built-in
  Kestrel, Windows native geometry, exact-HWND WGC/D3D11 capture on the Windows 10
  1903+ target, bounded GDI+ JPEG encoding/decoding and encoded frame metadata.
  No WinForms/WPF UI types or controls are referenced by the compiled Core assembly.
  Microsoft.WindowsDesktop.App is the shared imaging runtime, not a sample dependency.
- LazyChromeWindowBridge.Extension: Chrome 120+ MV3 extension, accepted Source version 0.3.1; publication remains pending checkpoint.
- Core package and SampleCaller product version: accepted Source version 0.3.1; publication remains a separate Controller operation.
- LazyChromeWindowBridge.SampleCaller: separate WinForms consumer using only public API.
- Core.Tests and PublicApi.Tests: deterministic/friend fixtures and a separate
  external consumer respectively. The sample/public consumer have no friend access.

BridgeRuntime is the public façade: StartAsync, LaunchAsync, GetSessions/GetSession,
GetWindow, SetWindowBounds, Park, Restore, SetShowInTaskbar,
StartMonitoring/StopMonitoring, SetSessionMonitoring,
GetMonitorState, GetLatestFrame and DisposeAsync. Coordinators and transport/native
implementation stay internal. Public snapshot/rectangle/options types are immutable
records; MonitorFrame supplies ReadOnlyMemory<byte> JPEG data rather than UI images.
DownloadChanged, GetDownloads and GetDownload expose the download contract below.

## Ownership
Launch validates absolute HTTP/HTTPS URLs, excluding embedded credentials. Each
session has independent random identity/capability even for the same URL.
The extension binds IDs from the exact local top-frame sender at
/lazy-chrome-window-bridge/bootstrap. A one-time native mapping acknowledgement
precedes initial navigation. Runtime marker: LazyChromeWindowBridge Session <id>;
native property: LazyChromeWindowBridge.SessionBinding.

The retained browserSessionId/windowId and HWND/PID/property are authority.
Navigation/current URL/title/pixels never choose a capture target. Active tabs may
change only inside that owned window. Close is terminal for that session, and a
reused/stale native identity cannot be moved or substituted. MV3 recovery within a
browser session preserves eligible bindings; full browser/caller restart takeover
is outside the contract.

## Native geometry
WindowSnapshot exposes session, WindowId, original launch/profile URL, exact native
identity, PlacementState, Current and Normal physical-pixel bounds, DPI, error and
placement generation. Unmapped returns null; stale/dead has Closed/no current bounds.

Profiles are normalized launch-URL identities, hashed filenames under
%LOCALAPPDATA%\LazyChromeWindowBridge\Geometry by default. Query and fragment stay
distinct. Stable Visible observation is debounced; ordinary and manual moves use
that same persistence path. There is no Auto mode or migration framework.

PARK retains Normal and moves the full native window above the topmost monitor with
zero intersection. Restore uses the exact retained identity and remembered Normal,
with deterministic reachable fallback when topology changes. Native moves verify
actual rectangle with bounded retries and 2px tolerance; no Chrome DIP conversion.
Placement generations describe native operations; they are not monitor identity and
do not invalidate frames across successful Restore/re-PARK of the same owned window.

SetWindowBounds applies only to a live Bound/mapped Visible native window.
It accepts valid physical dimensions and reachable negative-coordinate placement;
PARKED or transitional states require Restore. Closed/unmapped/stale identities fail.
Get immediately after Set may show the prior Normal until observer stabilization.
Normal shutdown restores PARKED windows and leaves Chrome open.

`SetShowInTaskbar(appSessionId, show)` is independent runtime policy for the exact
owned HWND. Default is shown. Hide removes WS_EX_APPWINDOW and applies WS_EX_TOOLWINDOW;
show restores the captured original extended style. Calls are idempotent, validate the
same live NativeIdentity, never activate/move/resize/monitor/rebind, survive PARK/RESTORE,
are not persisted to geometry, and restore the original style on normal disposal while
the owned window is still alive.

## Human-only monitoring
Global Start from stopped requests all live Bound sessions with a valid native identity and mapped
Visible or Parked window. Both placements use Waiting/Live/Error/Disconnected monitor
states and the same JPEG path; native placement is separately available through GetWindow.
Selection affects only Park/Restore. Each session retains independent
connection/generation/native identity, latest image, counters and error. Session
capability, connection, generation, WindowId and native identity must match before
and after decode. A capture failure cannot corrupt ownership/Normal or stop peers.

`CaptureOptions.Mode` is additive and defaults to `CaptureMode.BrowserViewport`, so
existing three-argument construction preserves the accepted behavior. BrowserViewport
uses only Page.getLayoutMetrics and Page.captureScreenshot. `CaptureMode.NativeWindow`
captures the exact validated `NativeIdentity.Hwnd` through
`IGraphicsCaptureItemInterop.CreateForWindow`; it has no picker, candidate enumeration,
replacement discovery or BrowserViewport fallback and does not attach the debugger.
Its frame metadata uses `Mode=NativeWindow` and `TabId=-1` as the documented
not-applicable sentinel; appSessionId, WindowId and NativeIdentity remain authority.

FPS accepts inclusive
1–30, defaults to 2, with roughly 2–30 recommended; 30 is a request ceiling, not a measured
throughput guarantee. Default output is max240×135, fixed JPEG quality70,
aspect-preserving and no capture upscale. Output bounds never change the native
window size, browser viewport or zoom. The native
window remains full size: experimentally shrinking it was rejected in R005.
The internal test-only shrink path preserves prior comparison/Normal-safety coverage
and is never called by production monitoring.

BrowserViewport frames are bounded, latest-only and sent over authenticated loopback
WebSocket. NativeWindow surfaces remain in Core: WGC supplies the exact HWND surface;
D3D11 VideoProcessorBlt crops to actual content and performs aspect-preserving no-upscale
resize on the GPU; only the bounded staging texture crosses to CPU memory, where GDI+
encodes JPEG quality70. A full-size native frame never crosses the loopback transport.
Visible↔Parked and placement-generation changes preserve monitor generation, latest
JPEG, control WebSocket and same-tab debugger attachment. StartMonitoring(options)
while enabled updates options in place and wakes the pump; the next practical iteration
uses new settings. An acquisition already in flight may finish with its prior settings.
Option updates do not clear the latest frame, reconnect or detach.
They never enable a session explicitly paused by the caller. A same-mode update preserves
the acquisition generation. A mode change advances that target generation, cancels and
disposes its old acquisition, rejects late frames and starts the selected backend without
creating/rebinding the owned session. Newly bound live sessions
are enabled by default while the global subsystem is started.

Monitoring policy belongs to the caller. PARK/RESTORE never automatically changes
Monitor ON/OFF: ON continues LIVE across either placement; OFF stays Paused across either.
SetSessionMonitoring(id, false) pauses only that live owned session. It advances only
that monitor generation to reject late in-flight frames, detaches its debugger and stops
capture/traffic. The existing restart-control WebSocket, ownership and geometry remain.
GetLatestFrame retains the exact last JPEG, Sequence and ReceivedAt as a frozen preview;
the snapshot State is Paused, never Live. There may be no JPEG if none was acquired.
Repeated OFF is idempotent. SetSessionMonitoring(id, true) reuses that waiting socket,
starts a new target generation, clears the frozen image and returns Waiting then Live
when fresh frames arrive. Peer generations/connections/attachments remain unchanged.
Healthy repeated ON is idempotent; explicit ON after real Error retries only that target.
The API throws InvalidOperationException while globally stopped or for unknown, unbound,
closed or stale native ownership, and ObjectDisposedException after disposal. It never
implicitly starts the global subsystem. Global Stop clears frozen and live JPEGs alike;
the next global Start is a batch restart and enables all live sessions again.
Real session/window/native lifetime, Stop, transport loss, disposal and explicit error
retry remain invalidation boundaries; stale identity/connection/generation frames are rejected.
Tab close permits same-owned-window reacquisition. User-canceled
debugging stays blocked until explicit new generation; errors retain their original
generation. Worker recovery requests currently eligible Visible and Parked targets.

SampleCaller renders ~200–300px independent Zoom tiles with identity, placement, monitor state,
age and error. It replaces only new frames, without deliberately blanking between
them. Small FPS/width/height/Apply controls exercise live StartMonitoring(options).
Selected-session Pause/Resume controls are independent of placement. Paused tiles keep
the image and frame number, displaying a static frozen marker instead of live age updates.
Stop/invalidated state clears obsolete pixels; placement alone does not. Layout size is independent
of captured JPEG maxima. Core supplies no PictureBox/Bitmap presentation API.

Stop disables monitoring, clears latest JPEGs, detaches BrowserViewport debugger targets,
disposes NativeWindow acquisition resources and stops frame traffic;
CapturingConnections becomes zero. Idle restart-control WebSockets remain and
Start reuses them. Normal async shutdown sends closing control, closes WebSockets,
stops capture, waits up to seven seconds for transport/debugger
cleanup including a five-second acquisition bound, releases WGC/D3D resources, restores
any LCWB-modified taskbar styles, restores Normal and stops Kestrel.
Forced termination cannot guarantee graceful cleanup.

## Download lifecycle
The extension observes only downloads.onCreated, downloads.onChanged and downloads.search
by an already observed ID. Public DownloadLifecycleEvent contains DownloadId, State
(Created/Complete/Interrupted), exact official Filename, optional interrupted Error and
ObservedAt. Created may have an empty filename; terminal events resolve the full current
DownloadItem. No appSessionId, URL, finalUrl or referrer is collected for this feature.

Observation is profile-global, grouped by exact bridgeId/origin. One representative
live session capability authenticates POST /lazy-chrome-window-bridge/api/downloads on
the existing Kestrel listener; it conveys no source ownership. Independent Bridges in
the same profile may each receive once. Each runtime accepts one browserSessionId.

The extension persists transitions before delivery in chrome.storage.session, and
retries through the existing worker/alarm path. Serialized delivery plus a monotonic
sequence and Core high-water mark deduplicate lost acknowledgements even after snapshot
eviction. Core preserves per-download order, isolates synchronous subscriber exceptions
and clears current state on disposal. Consumers must return promptly and queue slow work.

Bounds: 128 observed IDs, 256 pending transitions, 16 Bridge destinations; overflow
retires the oldest terminal stream first, otherwise oldest active stream, including
its pending events. Each runtime retains at most 256 latest download snapshots.
Filename/error bounds are 1024/128 characters, route bodies at most 16 KiB, requests
three seconds, up to 16 deliveries per Bridge per flush. See docs/downloads.md for
exact recovery, loss and lifetime boundaries. No arbitrary Chrome history is replayed.

Chrome Complete is authoritative for browser completion and is final in this stream.
The product never opens, scans, validates or moves a file. Consumers validate expected
directory/name, existence, stable size/time and exclusive access before moving it.
The broad downloads permission is intentionally used only for observation/search;
absolute filenames are sensitive and are not logged by production code.

## Security, scope and validation
IPv4 loopback only, ephemeral port, strict Host check, per-session256-bit capability,
no website CORS grant, bounded requests/frames, no redirects and no secret logging.
Same-user malicious processes are outside this security boundary. Chrome debugger
permission and target contention are explicit limitations. LCWB-owned Chrome launches
include --silent-debugger-extension-api exactly once, alongside the existing launch flags.
This is best-effort Chrome-dependent infobar suppression, not an extension permission
change or a security guarantee. If Chrome ignores it, a notice may appear and the
monitoring path still works. Reusing an already-running profile may retain that process's
original flags. BrowserViewport commands remain Page.getLayoutMetrics/Page.captureScreenshot;
NativeWindow does not use CDP capture.
This is not extension-side suppression, and debugger permission remains broad.
Visual infobar absence was not established in acceptance and is not guaranteed.
The known third-display initial offscreen capture timeout remains unresolved;
passing runs do not establish a fix or justify weaker timeouts/assertions.

No DOM/Runtime/Network extraction, OCR/semantic analysis, webpage completion detection,
remote input, recording, cloud, telemetry, provider framework, installer or updater.
R013 introduces no third-party runtime package. Historical GitHub/NuGet 0.1.0 and
0.2.0 releases and the submitted CWS 0.2.0 artifact/review remain unchanged.

Scripts/Test-All.ps1 is the stable validation entry. It restores/builds and runs Core,
external public-API and extension tests. With a supplied Chrome for Testing executable,
it runs download acceptance, session/native/BrowserViewport regressions and, when the
OS supports it, NativeWindow exact-composition, mixed-placement, pause/resume, taskbar
isolation and shutdown acceptance; -Gui supplies the actual sample fixture.
Assertions preserve R005 coverage.
Source name/security/hygiene auditing uses an explicit immutable-history allowlist.
Logs remain ignored flat Scripts/Outputs files. See docs/testing.md and the current
transient CHANGELOG for exact run results, identities and limits.
