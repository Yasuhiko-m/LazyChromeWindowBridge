# LazyChromeExtension — Project Definition

## Identity
- ProjectID: `LazyChromeExtension`
- ProjectName: `LazyChromeExtension`
- SourcePath: `C:\LazyAIDeckProjects\LazyChromeExtension`
- Project Data Path: `C:\LazyAIDeckData\Projects\LazyChromeExtension`

ProjectID is immutable. Do not substitute display name or folder for it. Web Project URLs and managed execution bindings are mutable Controller/runtime state, not stable Source identity.

## Purpose
Establish Windows integration between a launching application and a Chrome
window containing a Web application. The initial target Web application is ChatGPT.

## Users
The Windows user operating the launched Web application, and developers using
CallerHarness to exercise the contract intended for a future real launching application.

## Product scope
The product consists of a C# reference/test caller and a Chrome extension.
The reference implementation binds a stable application-session identity to a launched Chrome
window, preserves that relationship through navigation, and restores window geometry
according to the launch URL/profile. Human-only visual monitoring provides pixels
from the owned window while it is away from the normal visible desktop area.
SPEC.md distinguishes established behavior, review candidates, and future behavior.

## Platform / runtime
Windows, C# / .NET 10 Windows Forms (`net10.0-windows`), and current Google Chrome
with a Manifest V3 extension. Development uses an installed Windows .NET 10 SDK;
the framework-dependent caller requires the .NET 10 Windows Desktop and ASP.NET Core
shared runtimes. Kestrel supplies the caller-owned loopback HTTP/WebSocket listener.
Chrome's debugger extension permission supplies bounded JPEG viewport capture;
the caller renders the pixels with built-in Windows desktop imaging facilities.

## Paths
Source: `C:\LazyAIDeckProjects\LazyChromeExtension`
Project Data: `C:\LazyAIDeckData\Projects\LazyChromeExtension`

Source is versioned product/project authority.
Project Data is Lazy AI Deck Controller operational space and not a second Codex root. WorkspacePath is legacy migration input only.

## Roles
Chat = decision/planning.
Codex = Source Executor.
Lazy AI Deck = local execution/observation/routing/state.

## Project documents
AGENTS.md
PROJECT.md
PLANS.md
Revisions.md
SPEC.md
Exchange-Protocol.md
CHATGPT-PARAMS.md
CHANGELOG.md (transient)

Legacy Projects may temporarily retain PLAN.md until VMR-authority migration completes.

## Git policy
Git mode: `ON` (local Source repository).

Use active Project governance for completion rules. Implementation changes remain
uncommitted for Chat review unless a task explicitly authorizes a local checkpoint.
Remotes and pushes require explicit authorization.

## Stable constraints
- Source is the only implementation root; Project Data is Controller-owned.
- Launch URL and application-session identity have distinct meanings. Later page
  navigation must not redefine session ownership during the launched window's lifetime.
- Monitoring imagery is for the human user only. Monitoring must not use DOM
  scraping, OCR, semantic image analysis, ChatGPT output extraction, or page-content automation.
- Caller/extension communication uses an authenticated loopback HTTP bootstrap and
  capability-authenticated WebSocket pixel transport; the
  established contract is specified in SPEC.md. Native placement uses built-in Windows APIs
  in the caller, with Chrome window ownership retained by the extension contract.
- Keep the foundation small and request extension permissions only for implemented needs.

## External dependencies
Use built-in Windows/.NET desktop facilities and Chrome extension APIs. No third-party
NuGet or JavaScript packages are adopted. Add external dependencies only when an
explicit task permits them; do not install SDKs or system software implicitly.

## Distribution
Development scaffold only: a locally built caller and an unpacked extension.
No installer, store publication, or public distribution is established.

Do not store moving Current Revision state in PROJECT.md.
