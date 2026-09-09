# Revisions

- Current VMR: `V0-M003-R004`

## Purpose
Source semantic Revision history and, after governance migration, Current VMR authority.

Git records actual Source changes when enabled.
CHANGELOG.md is only the current Codex Task handoff.
Workspace Revisions/ is separate Controller-owned rollback/evidence data.

## Rules
- Current VMR identifies the last established Project Revision.
- Advance Current VMR only when the Project accepts the Revision as established.
- Do not duplicate full Git diffs when Git is authoritative.
- Record why the Revision existed, what became true, build/test evidence, remaining issues, and optional Git commit.
- Same-purpose retries use suffixes such as R001_1.

## History

### LazyChromeExtension_V0-M001-R000 — Starter Pack bootstrap
Purpose:
Materialize the Starter Pack governance baseline before the first project-specific Revision.

Result:
The Project has its initial governance documents and an operational bootstrap VMR.

Build / Test:
No Codex implementation or test Revision is claimed by this bootstrap entry.

Remaining:
Chat defines the first project-specific Revision after creation.

Git Commit:
N/A

### LazyChromeExtension_V0-M001-R001 — project-bootstrap
Purpose:
Establish the first project-specific Source foundation: a minimal Windows C#
CallerHarness, a minimal Chrome Manifest V3 extension, and the adopted project
documentation/roadmap.

Result:
Accepted by Chat. The project now contains `LazyChromeExtension.sln`, Windows
Forms `src/CallerHarness`, minimal permission-free MV3 `src/Extension`, adopted
`PROJECT.md`, `SPEC.md`, and `PLANS.md`, and a local Git Source repository.
Session binding, IPC, geometry, PARK/offscreen behavior, capture, and monitoring
remain unimplemented.

Build / Test:
Accepted implementation evidence (not rerun for this authority-only checkpoint):
- CallerHarness: `net10.0-windows`.
- Restore: PASS.
- Build: PASS, 0 warnings / 0 errors.
- GUI smoke launch/respond/normal-close: PASS.
- MV3 JSON validation: PASS.
- Service-worker JavaScript syntax validation: PASS.
- Chrome unpacked-extension load remains a manual acceptance check.

Remaining:
M001 continues. The next Revision is not predefined here.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M001-R002 — session-window-binding

Purpose:
Establish deterministic caller-session ownership of launched Chrome windows
independently of subsequent page navigation.

Result:
Accepted by Chat. CallerHarness owns the session identity and loopback bridge;
the extension binds the exact sender Chrome Window. The launch URL remains immutable
session metadata, and later navigation never redefines ownership. Simultaneous
same-URL sessions remain distinct. Window close terminates only its own session;
normal MV3 service-worker restart rehydrates active bindings. Geometry, PARK, and
monitoring are not part of R002.

Build / Test:
Accepted evidence: .NET build PASS (0 warnings / 0 errors); caller/HTTP checks
26 PASS; extension tests 6 PASS; Chrome for Testing integration PASS; one-session
navigation PASS; concurrent same-URL sessions PASS; Window-close PASS; MV3 worker
stop/restart PASS; Windows GUI acceptance PASS.

Remaining:
M001 is complete. M002 begins with geometry / PARK / RESTORE.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M002-R003 — geometry-park-restore

Purpose:
Complete launch-profile geometry persistence and exact PARK / RESTORE for the
bound Chrome Window.

Result:
Accepted by Chat. Geometry belongs to the original launch URL and normal placement
survives navigation. Exact HWND mapping is established before leaving the caller's
bootstrap. Native PARK moves that Window outside every active monitor; logical
PARK state prevents normal geometry contamination. RESTORE returns the same Window
to remembered normal placement. Concurrent sessions remain isolated and topology
fallback is deterministic. Monitoring remains unimplemented.

Build / Test:
Accepted evidence: build PASS (0 warnings / 0 errors); 57 caller checks PASS;
8 extension checks PASS; real negative-coordinate hardware PASS; geometry
save/relaunch PASS; PARK isolation PASS; navigation while PARKED PASS; RESTORE
PASS (maximum measured error 0 pixels); relaunch after PARK PASS; GUI acceptance
PASS. Mixed-DPI and monitor-removal cases have deterministic simulated coverage,
not physical hardware evidence. M001 regressions remain passing.

Remaining:
M002 is complete. M003 begins.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M003-R004 — human-monitor-integration

Purpose:
Provide a practical human-only live preview of the exact PARKED owned WebApp window
and validate the complete launch/binding/geometry/monitor/restore lifecycle.

Result:
Accepted by Chat. Chrome debugger viewport JPEG capture and authenticated loopback
WebSocket transport provide fresh images while the exact native window is fully
outside every monitor. Session/window/native identity and selection generations
prevent cross-session pixels. Navigation, PARK/RESTORE and MV3 recovery preserve
ownership. Normal caller shutdown detaches capture and restores parked geometry.
The accepted default is 2 fps / maximum 960×540 / JPEG quality 70. Chrome debugger
permission and its normal debugging notice are an accepted tradeoff. Product image
handling is pixels-only: no DOM/OCR/semantic/output extraction or page decisions.
Native PrintWindow was rejected after empirically freezing while PARKED.

Build / Test:
Build PASS, 0 warnings / 0 errors; 73 caller checks and 14 extension checks PASS;
all R002/R003 regressions PASS; real fully offscreen changing frames, A/B isolation,
parked navigation, same-HWND RESTORE (measured 0 px), MV3 worker recovery, capture
cleanup and actual WinForms GUI acceptance PASS. Four practical FPS/resolution
configurations were measured. Mixed-DPI/monitor removal remain simulated coverage;
normal authenticated Chrome/ChatGPT smoke was skipped without accessing that profile.

Remaining:
M003 complete. M004 final runtime tuning/multi-window/manual geometry begins.
M005 productization/rename/refactor is roadmap only and has no Revision assigned.

Git Commit:
Recorded externally by Git checkpoint; see repository history.
