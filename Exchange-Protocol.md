# Exchange Protocol

This installed Project document owns the package exchange contract between Chat,
Codex, and Lazy AI Deck. Do not move required wire rules solely into Chat Rule.

## Identity and roots

Every package identifies one exact `ProjectID` and full VMR. `SourcePath` is
Codex's only working root. Project Data under the selected Data Root is Controller-owned operational state
and is never a second Codex root.

Active protocol discriminators are:

```text
_toCodex
_toLazyAIDeck
```

`_patch` is prohibited: it is neither a package discriminator nor a current
completed-file route. `_toPwsh` is reserved future design only, not an active
discriminator or mutation route.

## Purpose and package presentation

Choose one concise, stable, filename-safe `[purpose]` for one task/package. It
is the same human task label used by the Task Card and the real wire filename
suffix; do not repeat ProjectID or VMR inside it.

Chat displays the canonical presentation labels:

```text
Vx-Mxxx-Rxxx : toCodex : purpose.zip
Vx-Mxxx-Rxxx : toLazy : purpose.zip
```

The display VMR omits ProjectID and same-purpose retry suffixes, using the base
revision in V-M-R order. These are human-facing Markdown-safe hyperlink labels,
not intake filenames.

The real wire filenames remain:

```text
LazyChromeExtension_[full VMR]_toCodex_[purpose].zip
LazyChromeExtension_[full VMR]_toLazyAIDeck_[purpose].zip
```

新規Chat発行の同目的retryは単一のflat issuance counterを使う:
`R001`、`R001_1`、`R001_2`。`R001_1_1` のように入れ子にしない。目的変更は
base Revisionを進める。既存の入れ子suffixは歴史的Package互換として読めるが、新規発行の形式ではない。

`toLazy` is display shorthand only and is never routing syntax. Runtime intake
requires the `_toLazyAIDeck_` discriminator.

## `_toCodex`

A `_toCodex` package contains required `task.md` and optional accepted
attachments. Its fixed metadata header appears in this exact order:

```text
Codex Model:
Codex Reasoning Effort:

ProjectID:
VMR:
Base VMR:
```

After that header, the body is opaque Chat-authored Markdown. The Server validates
fixed metadata, filename identity, current VMR/base compatibility, model and
reasoning capability, and accepted attachment boundaries before it gives the body
to Codex. Attachments never add `WorkspacePath` as a Codex root.

Production managed execution is ProjectID-scoped WSL managed Codex Exec. The
Server resumes the exact persisted managed Thread for one staged task and fails
closed for missing/mismatched Thread or lifecycle evidence. There is no automatic
fallback to Windows Codex App Server, no fresh-thread fallback, and no extra
writable root.

## `_toLazyAIDeck`

A deterministic Controller package contains:

```text
command.json             required
params/                  optional bounded command input
result.md                Server-reserved terminal result
```

`command.json` has `schemaVersion: 1`, exact `projectId`, and a non-empty ordered
command list. Commands stop at the first failure. Current command types are:

- `scripts-run` — an exact public Project Data Script Catalog target.
- `powershell` — a bounded package-local PowerShell file through `-File`.
- `deck-command` — only an exact Server-advertised operation with `params: []`; it is never a generic command/path route. Current targets are `migrate-project-data`, `validate-revision`, and read-only `lazy-blueprint`, whose Markdown is returned through Standard Output.

The managed intake is `<DataRoot>\Downloads\Inbox`, not ordinary
Downloads. The Server owns extraction, Project routing, Processing, Completed,
Failed, and the one Server-generated terminal `result.md` capsule. A package
success/failure result is Controller execution evidence, not authorization for an
unbounded local operation.

両方のroutable package discriminatorで、exact wire filenameの重複保護はlifecycle-awareである。
CompletedまたはProcessingは再実行せずrejectし、保持済みFailed executionは新しいattemptとして再発行できる。
Failed archiveは不変のhistoryとして残し、duplicate-rejection archiveは最新の実行結果を置き換えない。

When a valid routable `_toCodex` or `_toLazyAIDeck` package has an exact terminal
archive below the Server Failed root, its matching Project Client may request
one-shot Explorer selection using ProjectID, original filename, and Failed archive
identity. The Server revalidates that exact direct child; selection never opens,
executes, retries, or mutates the ZIP. A valid duplicate download fails as
`duplicate_package` without executing its second copy and follows the same rule.

## Apply Patch

Completed Source/Project Data files use the current normal route:

```text
_toLazyAIDeck
  -> scripts-run
  -> apply-patch.bat
```

`command.json` targets exact public `apply-patch.bat` with `params: []`. The
package payload is:

```text
params/apply-patch/manifest.json
params/apply-patch/Source/       optional
params/apply-patch/ProjectData/  optional after Project Data migration; `Workspace/` is legacy-only
```

Manifest `schemaVersion` is `1` and it declares exact `projectId`, `sourceFiles`,
`projectDataFiles` (or legacy `workspaceFiles` before explicit migration), and SHA-256 for every member. Paths are relative only; Source-
only, Project Data-only, and mixed payloads are allowed; deletion is unsupported.
The Server supplies the registered ProjectID, SourcePath, and exact selected Project operational root;
validated payload root only to exact `apply-patch.bat`. Ordinary Scripts receive
no Apply Patch handoff. The Script validates strict membership/hashes and applies
only below those registered roots with bounded rollback. `apply-local` is not a
current Pack capability.

## Future `_toPwsh`

`_toPwsh` may become a bounded read-only Windows host-inspection route only after
separate implementation, tests, and real Windows acceptance. It is never a
generic shell escape hatch and never a Source/Workspace mutation route.
