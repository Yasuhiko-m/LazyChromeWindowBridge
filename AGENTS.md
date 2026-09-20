# AGENTS.md

## Project
ProjectID: `LazyChromeExtension`

Codex is the Source Executor. Its only Project working root is the exact Source working root provided for the Task.
Project Data under the selected Data Root is Controller-owned and is not a second Codex root. WorkspacePath is legacy migration input only.

## Current Task
Work from the current Task purpose, requirements, scope, and completion criteria.
Inside the established specification boundary, choose implementation details as needed.
If Task purpose or an external specification boundary must change, stop and report.
Complete the adopted arrival state, including related implementation, tests and
necessary UI cleanup. A substantial 30–60 minute Task is sizing guidance, not a
runtime SLA. Do not turn it into cosmetic micro-Revisions or extra confirmation
stops; safety comes from design, authority boundaries and validation. Split only
at a real purpose, authority or deployment boundary.

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
Do not create tests merely because Source changed or validation selection has no owner.
Prefer existing Contract/Regression tests; add a test only for a durable product/security/data-integrity contract or a concrete recurrence-worthy regression.
Use Acceptance/Canary for real Windows, browser, WSL, process, packaging, OAuth, tunnel, and filesystem/AV behavior when that environment is the authority.
Full regression belongs only to an explicit broad boundary such as a milestone close, release candidate, cross-cutting refactor, or test/validation architecture Task.
Do not repair test/validation architecture outside the current Task scope.

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

## Managed Source projection

Windows absolute `SourcePath` is physical Source authority. Managed Codex uses only
`/LazyAIDeckProjects/<DeckName>/<ProjectID>` as its execution cwd. This WSL
projection is execution-only state backed by the same Windows directory; it is
not a second Source or Project Data root. The initial DeckName is `default`.
The Server converts and checks the Windows backing path, then uses bounded
`wsl.exe -d <distribution> -u root` operations to establish an idempotent bind
mount and its exact persistent `/etc/fstab` entry. It rejects conflicting mounts,
nonempty targets and symlink targets instead of overwriting them. Explicitly
disabled WSL fstab loading fails projection checks. The normal Codex user executes tasks;
no sudo password is requested or stored. Direct `/mnt/<drive>/...` managed cwd,
the retired `/source` contract, temporary resolver bridges and Git-check bypasses
are not supported. WSL drive paths remain internal backing-path inputs only.

Managed Thread persistence includes DeckName, ProjectID, normalized Windows
SourcePath, distribution, Codex home/executable and organized projection path.
A legacy or mismatched mapping bootstraps a new Thread. Persistence is atomically
replaced only after bootstrap succeeds with a valid Thread ID and completed turn;
failure preserves the previous entry. Matching mappings still verify the current
projection before reuse. Sandbox defaults remain `workspace-write` with explicit
per-Project `danger-full-access` opt-in.
