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
manual physical-pixel placement, human-only continuous JPEG previews and read-only Chrome
Download Manager lifecycle notification. Consumers are
Windows application developers and the humans using their launched web applications.
Lazy AI Deck is one possible consumer, not the owner/container of this runtime.

## Product structure
Core is a net10.0-windows library with a public BridgeRuntime façade. It has no
WinForms/WPF UI type dependency. The MV3 extension handles exact binding, worker
recovery, pixel capture and official download observation. SampleCaller is a separate WinForms public-API consumer.
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
- Owned Chrome launches request best-effort silent debugger infobar suppression;
  debugger permissions and the Page.getLayoutMetrics/Page.captureScreenshot boundary remain.
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

Local Git is enabled. Candidate revisions remain unstaged/uncommitted for Chat review
unless the active task explicitly authorizes an accepted-baseline checkpoint.
Current accepted Source is V1-M006-R012, retaining Chat-accepted R010 continuous
Visible/Parked monitoring and R011 per-session pause/resume in the combined R011
checkpoint; GitHub v0.1.0 and NuGet 0.1.0 are older baselines. M004/M005 are
Complete and V1 remains In Progress. Chrome Web Store live submission is separate.
Chat accepted V1-M006-R012 distribution preparation; M006 is Complete and Current
VMR is V1-M006-R012. Version 0.2.0 is established as a prepared, unpublished candidate.
Publication artifacts must be rebuilt from the exact accepted checkpoint.
The third-display initial offscreen capture timeout remains unresolved; silent launch
suppression is best-effort and visual infobar absence was not established in acceptance.
The project is early-release software licensed under MIT, copyright 2026 Yasuhiko Mori,
derived from the existing repository author identity. R012 preparation is accepted;
checkpoint and publication require separate authorization. No installer/updater or
Web Store distribution is established; the extension ZIP is a generated release asset.
