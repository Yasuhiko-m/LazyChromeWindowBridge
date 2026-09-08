# AGENTS.md

## Project
ProjectID: `LazyChromeExtension`
SourcePath: `C:\LazyAIDeckProjects\LazyChromeExtension`

Codex is the Source Executor. Its only Project working root is SourcePath.
Project Data under the selected Data Root is Controller-owned and is not a second Codex root. WorkspacePath is legacy migration input only.

## Current Task
Work from the current Task purpose, requirements, scope, and completion criteria.
Inside the established specification boundary, choose implementation details as needed.
If Task purpose or an external specification boundary must change, stop and report.

## ProjectID Guard
Before Source changes, compare ProjectID from the Task, this file, and PROJECT.md.
Do not start changes unless the identity matches exactly.

## Source authority
Prefer current Source assets:
- AGENTS.md
- current Task
- directly related SPEC.md
- related Source/config/tests
- PROJECT.md when stable Project definition is needed
- PLANS.md for Version/Milestone context when present
- Revisions.md for historical/current-VMR context when present
- legacy PLAN.md only while the current Project still uses it
- Exchange-Protocol.md for exchange details

CHANGELOG.md is a transient current-task handoff, not cumulative history or specification authority.

## Fast Path
1. AGENTS.md
2. Current Task
3. Directly related SPEC.md
4. Related Source/config/tests
5. PROJECT.md when needed
6. PLANS.md when roadmap context is needed
7. Revisions.md only when history is needed
8. Legacy PLAN.md only when current implementation requires it

## Source / Controller boundary
Source-owned: product implementation, Source build/test/migration, development tooling, product templates/generators.
Controller-owned: launch/stop shortcuts, log viewer, patch/application helper, runtime-management shortcuts, Project Data Script Catalog operational scripts.
Do not create permanent Source-local substitutes for Controller-owned assets.
Remove temporary Source probes before completion unless the Task makes them permanent Source assets.

## Script authoring
Before creating or editing a Script, determine its Source/Controller ownership and
follow the portable contract in `Scripts/SCRIPT-AUTHORING.md`. Codex edits only
Source-owned assets; Project Data operational Scripts remain Chat/Controller-owned.

## Dependencies
Add external dependencies only when the current Task explicitly permits them.

## Editing and deletion
Use normal Codex Source editing.
A Codex sandbox editing tool named `apply_patch` is unrelated to the retired
Lazy AI Deck `_patch` exchange discriminator. Completed Controller file
application uses `_toLazyAIDeck` `scripts-run` with public `apply-patch.bat`.
A Windows sandbox fallback may write only to a resolved SourcePath child and may not broaden permissions or roots.
Resolve every deletion to a concrete bounded SourcePath path first. Stop on unexpected or partial deletion.

## Attempts
Use at most five materially distinct attempts for the same problem. Record attempts and verification in CHANGELOG.

## Git
Git is actual Source-change evidence when enabled.

Normal Revision verification is Task-scoped:
- Use the Revision Journal start tree -> end tree as the authoritative Task delta when available.
- If the Task starts on a dirty worktree, inherited changes are outside the current Task delta.
- Do not make full-worktree `git diff --check` an unconditional normal-Task completion gate.
- Validate whitespace on the Task delta; if direct A->B checking is unavailable, use scoped changed-path checks plus pre/post dirty-state comparison.
- Inherited whitespace or line-ending defects do not fail the current Task unless this Task introduced them.
- Do not silently repair inherited whitespace or line endings in unrelated work.

Do not commit merely because `.git` exists.
Normal Codex completion does not implicitly commit or push.
Create a local VMR commit only when active Project governance/current Task explicitly requires a Codex-owned Git checkpoint.
Never push as an implicit Codex completion action. Never force-push as normal workflow.
Full-worktree `git diff --check` belongs to explicit checkpoint/push hygiene, normally after Chat acceptance and Controller-owned.
CHANGELOG.md remains transient and uncommitted.

## Build / Test
Run the Project build/tests required by the current Task after implementation changes.
Documentation/template-only work does not require unrelated builds unless requested.

## Completion handoff
Completely overwrite Source CHANGELOG.md in UTF-8 with the current Task only:

ProjectID:
VMR:
Task:
Status:

Changed Files:
- ...

Build / Test:
...

Attempts:
None

Warnings / Remaining Issues:
...

Status is Complete, Blocked, or Failed.
The final Codex response must agree with this handoff.

## Stop
Stop further Source changes if ProjectID cannot be confirmed, a specification decision is required, an unapproved dependency is required, a deletion cannot be bounded, five attempts are exhausted, or completion cannot be achieved safely.
