# LazyChromeExtension — Current Specification

## Role

This document is the Project's current adopted specification. It records the
currently accepted behavior, boundaries, terminology, and transitional
compatibility rules without turning future plans into current behavior.

## Authority

- `PROJECT.md` — stable Project definition.
- `SPEC.md` — current adopted specification.
- `PLANS.md` — roadmap when this Project uses normalized authority.
- `Revisions.md` — semantic Revision history and Current VMR after its authority
  migration.
- `PLAN.md` — legacy V0 Current VMR authority only while that authority remains
  active.
- `AGENTS.md` — permanent Codex execution guidance.
- `Exchange-Protocol.md` — package exchange contract.
- `CHATGPT-PARAMS.md` — Chat model/reasoning selection policy.
- `CHANGELOG.md` — one transient current-task handoff, not cumulative authority.

## Project boundary

ProjectID is `LazyChromeExtension`. SourcePath is `C:\LazyAIDeckProjects\LazyChromeExtension` and is Codex's only
working root. The Controller owns the derived Project Workspace
`C:\LazyAIDeckData\Projects\LazyChromeExtension`; it is not a second Codex root and is not a Git repository.

## Current adopted behavior

The foundation has two independent components: a Windows C# WinForms caller at
`src/CallerHarness` and a Chrome Manifest V3 extension at `src/Extension`.
`LazyChromeExtension.sln` builds the caller with `net10.0-windows` and no third-party
packages. CallerHarness is the reference/test application for the contract a future
real launching application will use; no communication contract is implemented yet.

The scaffold presents a launch URL field (initially `https://chatgpt.com/`), a Launch
button, and a status label. Launch only changes the status to explain that session
launch is not implemented. It does not open Chrome or contact the URL.

The extension contains a manifest and a service worker that logs installation.
It requests no permissions or host access and has no content scripts. The extension
package version is packaging metadata, not an established Project VMR.

These scaffold changes are submitted for Chat review. Completion of implementation
does not establish acceptance; the existing PLAN.md baseline remains authoritative.

## Adopted boundaries and future intent

- A future caller-initiated session will bind a stable application-session identity
  to the Chrome window opened for it. The launch URL is significant; later navigation
  must not redefine session ownership for the lifetime of that window.
- Window position and size will later be stored/restored according to launch URL/profile.
- A later milestone will investigate visual monitoring while the window is moved away
  from the normal visible desktop area. Imagery is intended only for the human user.
- No ChatGPT DOM/output extraction is part of the product. Monitoring must not use
  DOM scraping, OCR, semantic image analysis, output extraction, or page-content automation.
- Exact session transport is undecided. Localhost/WebSocket and Native Messaging
  are not selected or implemented.
- Special PARK/offscreen behavior is not an adopted implementation mechanism.
  Geometry persistence, window movement, HWND lookup, SetWindowPos, tabCapture,
  screenshots, video capture, and monitoring are not implemented in this foundation.

## Local validation and acceptance

From the registered Source root on Windows:

```powershell
dotnet restore .\LazyChromeExtension.sln
dotnet build .\LazyChromeExtension.sln --no-restore
dotnet run --project .\src\CallerHarness\CallerHarness.csproj --no-build
```

Check that the caller opens with `Status: Ready` and Launch shows the placeholder
status. Browser launch and session binding are not acceptance expectations yet.

For manual extension acceptance, use Chrome's Extensions page, enable Developer
mode, choose Load unpacked, and select the registered Source's `src\Extension`
directory. Confirm no extension errors and inspect the service worker if needed.
An idle service worker may stop normally. Chrome profile changes belong to this
explicit user acceptance step, not automated scaffold validation.
