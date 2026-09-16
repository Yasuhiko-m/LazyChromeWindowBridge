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
Current accepted base and Current VMR are V1-M011-R022. M009 Chrome launch options,
M010 capture pipeline/preview stability and M011 v0.3.1 release preparation are Complete;
V1 remains In Progress. Source version 0.3.1 is established. BrowserViewport remains the
compatibility default; NativeWindow uses exact-HWND WGC/D3D11 capture, taskbar visibility
is independent exact-HWND runtime policy, and PARK/RESTORE remains separate from monitoring.
R019 is an infrastructure-only restoration of the Controller validation map. R021 adopts
stable .NET SDK 10.0.4xx servicing selection (10.0.401 floor and latestPatch roll-forward)
for release tooling only. R022 is the documentation-only closeout of the completed GitHub
v0.3.1 Release and successful NuGet OIDC workflow/push from checkpoint
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd`; NuGet Gallery indexing was not independently
measured. Accepted 0.3.1 Source behavior remains unchanged. The submitted CWS 0.2.0
artifact/review remains unchanged and CWS 0.3.1 remains unpublished.
The third-display initial offscreen capture timeout remains unresolved; silent launch
suppression is best-effort and visual infobar absence was not established in acceptance.
The project is early-release software licensed under MIT, copyright 2026 Yasuhiko Mori,
derived from the existing repository author identity. No installer/updater is established.
