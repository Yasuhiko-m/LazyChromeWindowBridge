# LazyChromeWindowBridge — Project Definition

## Identity
- ProjectID: `LazyChromeExtension`
- ProductName: `LazyChromeWindowBridge`
- SourcePath: `C:\LazyAIDeckProjects\LazyChromeExtension`
- Project Data Path: `C:\LazyAIDeckData\Projects\LazyChromeExtension`

ProjectID and registered Source are immutable governance identity. Product/repository
name is separate. Project Data is Controller-owned and is not an implementation root.

## Purpose and users
Provide an independent reusable Windows application-to-Chrome bridge for deterministic
session/window/native ownership, launch-URL Normal geometry, offscreen PARK/RESTORE,
manual physical-pixel placement, human-only continuous JPEG previews, independent
per-owned-window taskbar visibility and read-only Chrome
Download Manager lifecycle notification. Consumers are
Windows application developers and the humans using their launched web applications.
Lazy AI Deck is one possible consumer, not the owner/container of this runtime.

## Product structure
Core is a dual-target net10.0-windows library with a public BridgeRuntime façade. It has no
WinForms/WPF UI type dependency. The MV3 extension handles exact binding, worker
recovery, BrowserViewport pixel capture and official download observation. On the
Windows 10 1903+ target, Core also captures an exact owned HWND through Windows
Graphics Capture and Direct3D 11. SampleCaller is a separate WinForms public-API consumer.
Core provides encoded frame bytes/metadata; presentation remains with the consumer.

Runtime dependencies are the built-in .NET10, ASP.NET Core and Windows Desktop
shared frameworks, native Windows APIs and Chrome120+. Desktop GDI+ supplies bounded
JPEG decoding without a Core UI control dependency. No third-party NuGet/npm package
is adopted. PowerShell7 and Node.js22+ support Source validation.

## Stable constraints
- Original launch URL is profile metadata; subsequent navigation never redefines ownership.
- Manual bounds apply only to the exact live Visible native window, in physical pixels.
- PARK/RESTORE are explicit; Normal cannot learn PARK geometry.
- Monitoring covers live owned Visible and Parked windows, independently per session and for human viewing only.
- The caller owns monitor policy. PARK/RESTORE never auto-toggles it. Per-session
  pause retains a frozen last JPEG; global Start/Stop remain batch controls.
- BrowserViewport is the default and preserves the extension debugger boundary of
  Page.getLayoutMetrics/Page.captureScreenshot. NativeWindow uses the exact retained HWND,
  requires Windows 10 version 1903 or later, and does not require a debugger attachment.
- Per-session taskbar visibility is runtime-only exact-HWND policy, independent of
  geometry and monitoring, and is restored on normal release when LCWB changed it.
- No DOM/OCR/semantic/output extraction, webpage completion detection or autonomous page decisions.
- Download notifications are profile-global, with no appSession attribution or URL/content
  collection. Chrome Complete is authoritative; filesystem safety checks belong to consumers.
- The downloads permission is broad but product use is read-only event/search observation.
  Absolute filenames are sensitive and are never logged by production code.
- Loopback transport is capability-authenticated; there is no cloud relay or telemetry.
- Keep public integration small; internal coordinators are not a consumer contract.
- No dependency installation, remote change or publication is implicit.

## Governance and distribution
Chat owns acceptance/planning; Codex executes Source work. PLAN.md/Revisions.md record
established VMR, PLANS.md the roadmap, SPEC.md the current contract, and CHANGELOG.md
the transient current-task handoff. AGENTS.md and Exchange-Protocol.md govern execution.
CHATGPT-PARAMS.md contains the inherited model-selection policy.

Local Git is enabled. Accepted Source revisions remain unstaged/uncommitted until the
independent checkpoint boundary explicitly stages the accepted task delta.
Current accepted base and Current VMR are V1-M013-R033. R027_1 remains the accepted M013
implementation/validation endpoint; R033 records its manual CWS 0.3.2 submission/pending
review state.
M009 Chrome launch options,
M010 capture pipeline/preview stability, M011 v0.3.1 release preparation, M012 bounded
key chord input and M013 release automation/version-coherent distribution are Complete;
V1 remains In Progress. Source version 0.3.2 is established.
BrowserViewport remains the
compatibility default; NativeWindow uses exact-HWND WGC/D3D11 capture, taskbar visibility
is independent exact-HWND runtime policy, and PARK/RESTORE remains separate from monitoring.
R019 is an infrastructure-only restoration of the Controller validation map. R021 adopts
stable .NET SDK 10.0.4xx servicing selection (10.0.401 floor and latestPatch roll-forward)
for release tooling only. GitHub v0.3.2 and immutable-tag NuGet OIDC publication completed
from checkpoint `d490bdbbfd6c98755388a64e288cc8ac8989a98a`; NuGet Gallery indexing for
0.3.2 was not independently measured. CWS 0.2.0 is general-public but does not match
Core/Extension 0.3.x; CWS 0.3.2 is submitted/pending review while CWS 0.2.0 remains
general-public. M013 establishes matching-version GitHub
Extension fallback, local version-driven preparation, immutable-tag Controller
publication allowlists and dormant optional existing-item CWS V2 normal-review dispatch
without committed credentials; manual CWS submission is the current operating path.
M012 permits one structured allowlisted
key/chord only for the exact owned active tab: acknowledgement confirms fixed CDP dispatch,
not page or Chrome-UI handling; no arbitrary automation or macro is provided.
The third-display initial offscreen capture timeout remains unresolved; silent launch
suppression is best-effort and visual infobar absence was not established in acceptance.
The project is early-release software licensed under MIT, copyright 2026 Yasuhiko Mori,
derived from the existing repository author identity. No installer/updater is established.
