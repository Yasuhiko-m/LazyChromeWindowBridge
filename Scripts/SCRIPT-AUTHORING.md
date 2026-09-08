# Script authoring contract

This document is the portable authority for Source and Project Data Scripts.

## Layout and ownership

- Public executable Script entries live directly under `Scripts/`.
- Private implementations and helpers live under `Scripts/Internal/`.
- Generated execution logs live only under the flat `Scripts/Outputs/` directory.
- Determine whether a Script is Source-owned or Project Data-owned before creating or editing it. Codex edits only Source-owned assets. Chat/Controller generates or applies Project Data-owned operational assets; do not create a permanent Source substitute for them.

## Interpreter and call boundary

- Identify the intended interpreter/runtime and the public-to-internal call boundary.
- Current supported Windows public wrappers use PowerShell 7 (`pwsh`) where required by the Project contract. Do not silently fall back to Windows PowerShell.
- Preserve structured arguments and the owned child process exit status.
- Quote and validate ordinary paths, including paths containing spaces.
- Make PowerShell/.NET overload selection and path usage unambiguous.

## Execution log

- One public Script invocation creates one `Scripts/Outputs/yyyyMMdd-HHmmssfff-ScriptName.log`.
- The timestamp is the first filename component; do not create VMR, Task, operation, or other subdirectories below `Scripts/Outputs/`.
- Do not add or accept an output/log-path parameter merely to control this convention.
- The owning Script resolves its own `Scripts` root, ensures `Scripts/Outputs/` exists, and opens and maintains the log.
- If the required log cannot be established, fail safely before an intended mutating operation begins.
- Record the complete diagnostic evidence needed for the invocation: Script decisions, owned child-process output, failures, and final exit status. Do not introduce secrets merely for logging.
- Helper/Internal output belongs to the owning public invocation log; helpers do not create unrelated nested log trees.
- The log is disposable immediate diagnostic evidence, not a long-lived Revision or Task archive.

## Terminal reporting

- Success returns exit code `0`; failure returns nonzero. A child failure must not be masked by later commands.
- Standard output/error may remain concise and useful; the full log need not be dumped again at completion.
- Always emit one concise logical log reference: `Output: Scripts/Outputs/<filename>`.
- Do not expose a physical log path through that terminal reference.
- Script code does not generate or own package `result.md`, Task Card lifecycle, or package lifecycle; those remain Controller responsibilities.

## Determinism and safety

- Use deterministic selection and tie-breaking when multiple filesystem candidates exist.
- Controller-invoked Scripts are non-interactive unless an adopted higher-level product contract explicitly owns interaction.
- Match mutation validation and rollback to the operation's blast radius.
- Prefer integration evidence from the actual intended Windows interpreter. When that runtime is unavailable, report the exact Controller-required Windows validation.
