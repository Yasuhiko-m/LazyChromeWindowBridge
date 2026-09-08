# Revisions

- Current VMR: `V0-M001-R001`

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
