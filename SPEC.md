# LazyChromeWindowBridge — Current Specification

ProjectID: LazyChromeExtension.
Source: C:\LazyAIDeckProjects\LazyChromeExtension.
Project Data is Controller-owned and is not another implementation root.

## Authority
Established baseline: **V0-M005-R006 — productize-refactor**, accepted by Chat.
PLAN.md and Revisions.md establish R006. M001–M005 and V0 are Complete.
Product name is LazyChromeWindowBridge; immutable ProjectID does not change.

R006 preserves the externally observable R005 contract. The prior accepted history
and measurements remain in Revisions.md and the R005 Git checkpoint. Current
maintainable product documentation is in README.md and docs/.

## Components and API
- LazyChromeWindowBridge.Core: net10.0-windows reusable library, built-in Kestrel,
  Windows native geometry, bounded GDI+ JPEG decoding and encoded frame metadata.
  No WinForms/WPF UI types or controls are referenced by the compiled Core assembly.
  Microsoft.WindowsDesktop.App is the shared imaging runtime, not a sample dependency.
- LazyChromeWindowBridge.Extension: Chrome 120+ MV3 extension, version 0.0.6.
- LazyChromeWindowBridge.SampleCaller: separate WinForms consumer using only public API.
- Core.Tests and PublicApi.Tests: deterministic/friend fixtures and a separate
  external consumer respectively. The sample/public consumer have no friend access.

BridgeRuntime is the public façade: StartAsync, LaunchAsync, GetSessions/GetSession,
GetWindow, SetWindowBounds, Park, Restore, StartMonitoring/StopMonitoring,
GetMonitorState, GetLatestFrame and DisposeAsync. Coordinators and transport/native
implementation stay internal. Public snapshot/rectangle/options types are immutable
records; MonitorFrame supplies ReadOnlyMemory<byte> JPEG data rather than UI images.

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
Placement generations invalidate frames across rapid Restore/re-PARK.

SetWindowBounds applies only to a live Bound/mapped Visible native window.
It accepts valid physical dimensions and reachable negative-coordinate placement;
PARKED or transitional states require Restore. Closed/unmapped/stale identities fail.
Get immediately after Set may show the prior Normal until observer stabilization.
Normal shutdown restores PARKED windows and leaves Chrome open.

## Human-only monitoring
Global Start requests all eligible PARKED sessions, at least four concurrently.
Visible reports ACTIVE with no requested capture, monitoring debugger or accepted
image traffic. Selection affects only Park/Restore. Each session retains independent
connection/generation/native identity, latest image, counters and error. Session
capability, connection, generation, WindowId and native identity must match before
and after decode. A capture failure cannot corrupt ownership/Normal or stop peers.

Only Page.getLayoutMetrics and Page.captureScreenshot are used. Default is 2 fps,
max240×135, JPEG quality70, aspect-preserving and no capture upscale. The native
window remains full size: experimentally shrinking it was rejected in R005.
The internal test-only shrink path preserves prior comparison/Normal-safety coverage
and is never called by production monitoring.

Frames are bounded, latest-only and sent over authenticated loopback WebSocket.
Restore invalidates/clears its pixels and wakes disabled control; in-flight late
frames are rejected. Tab close permits same-owned-window reacquisition. User-canceled
debugging stays blocked until explicit new generation; errors retain their original
generation. Worker recovery requests only currently eligible PARKED targets.

SampleCaller renders ~200–300px independent Zoom tiles with identity, ACTIVE/LIVE,
age and error. It replaces only new frames, without deliberately blanking between
them. Stop/Visible/invalidated state clears obsolete pixels. Layout size is independent
of captured JPEG maxima. Core supplies no PictureBox/Bitmap presentation API.

Normal async shutdown stops capture, waits up to seven seconds for transport/debugger
cleanup including a five-second acquisition bound, restores Normal and stops Kestrel.
Forced termination cannot guarantee graceful cleanup.

## Security, scope and validation
IPv4 loopback only, ephemeral port, strict Host check, per-session256-bit capability,
no website CORS grant, bounded requests/frames, no redirects and no secret logging.
Same-user malicious processes are outside this security boundary. Chrome debugger
permission/normal notice and target contention are explicit limitations.

No DOM/Runtime/Network extraction, OCR/semantic analysis, completion detection,
remote input, recording, cloud, telemetry, provider framework, installer or updater.
No third-party package, OSS license, remote/repository creation or publication is
introduced by this candidate.

Scripts/Test-All.ps1 is the stable validation entry. It restores/builds and runs Core,
external public-API and extension tests. With a supplied Chrome for Testing executable,
it runs session/native/monitor regressions then one ACTIVE plus four PARKED real
acceptance; -Gui supplies the actual sample fixture. Assertions preserve R005 coverage.
Source name/security/hygiene auditing uses an explicit immutable-history allowlist.
Logs remain ignored flat Scripts/Outputs files. See docs/testing.md and the current
transient CHANGELOG for exact run results, identities and limits.
