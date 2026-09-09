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
manual physical-pixel placement and human-only PARKED thumbnails. Consumers are
Windows application developers and the humans using their launched web applications.
Lazy AI Deck is one possible consumer, not the owner/container of this runtime.

## Product structure
Core is a net10.0-windows library with a public BridgeRuntime façade. It has no
WinForms/WPF UI type dependency. The MV3 extension handles exact binding, worker
recovery and pixel capture. SampleCaller is a separate WinForms public-API consumer.
Core provides encoded frame bytes/metadata; presentation remains with the consumer.

Runtime dependencies are the built-in .NET10, ASP.NET Core and Windows Desktop
shared frameworks, native Windows APIs and Chrome120+. Desktop GDI+ supplies bounded
JPEG decoding without a Core UI control dependency. No third-party NuGet/npm package
is adopted. PowerShell7 and Node.js22+ support Source validation.

## Stable constraints
- Original launch URL is profile metadata; subsequent navigation never redefines ownership.
- Manual bounds apply only to the exact live Visible native window, in physical pixels.
- PARK/RESTORE are explicit; Normal cannot learn PARK geometry.
- Monitoring is PARKED-only, independent per session and for human viewing only.
- No DOM/OCR/semantic/output extraction, completion detection or autonomous page decisions.
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
The project is pre-release and prepared for a private repository baseline; repository
creation/push waits for explicit accepted-task authorization. An eventual public
license is not yet selected. No installer/store/package distribution is established.
