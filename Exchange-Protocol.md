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

## Chat preflight before user handoff

Chat first constructs the intended filename and exact metadata/content, then calls
the matching read-only MCP preflight itself. `_toLazyAIDeck` uses
`deck_preflight_controller_package` with `command.json` and logical member/hash
metadata; `_toCodex` uses `deck_preflight_codex_task` with `task.md` and optional
attachment metadata. If preflight fails, Chat corrects the prospective package and
repeats MCP preflight internally. Only PASS permits Chat to generate and present the
ZIP to the user. The user is never asked to download or run a separate "preflight
package". Real Server ZIP intake repeats the same authoritative validation and
remains final authority.

Controller `command.json` has no `Base VMR`; its filename VMR must equal the
registered Project's current VMR. Codex `task.md` retains required `Base VMR`, which
must equal the registered Project's current VMR before dispatch.

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
to Codex. `deck_preflight_codex_task` applies that same metadata validator without
creating a Task or reading attachment bytes; supplied attachment paths/sizes/hashes
are bounded, and real intake remains responsible for byte/member validation.
Attachments never add `WorkspacePath` as a Codex root.

Each accepted `_toCodex` package attempt receives one Server-owned Guid-N
AttemptID. That exact identity is also the managed Codex TaskID from package
Detected/Processing through managed Queued/Starting/Running/terminal state, so no
placeholder binding or second identity exists. Once the exact managed Task exists,
its state is Task Card authority and later package-finalization notifications cannot
regress or duplicate it. A later retry receives a new AttemptID/TaskID even when the
wire filename is unchanged.

For an accepted attachment Task, the Server uses that package attempt's canonical Guid-N TaskID,
creates `/tmp/lazy-attachments/<taskid>/` in the configured WSL distribution,
and transfers only the validated files there. The managed Exec keeps its primary
`--cd` working directory at `/LazyAIDeckProjects/<DeckName>/<ProjectID>` and
receives exactly that Task-local directory through Codex CLI `--add-dir`.
The Server validates physical absolute Windows Source authority and establishes
the persistent execution-only WSL projection with bounded root operations. It
never adds Project Data, Workspace, an arbitrary host root, or the broader staging
root. The final persisted Task prompt and Codex input append that exact Linux
attachment directory. Task-local staging is not terminal-cleaned; Server startup
performs one safe stale-cache pass for only direct 32-hex directory children at
least 24 hours old. Attachments never add `WorkspacePath` as a Codex root.

Production managed execution is ProjectID-scoped WSL managed Codex Exec. The
Server resumes the exact persisted managed Thread for one staged task and fails
closed for missing/mismatched Thread or lifecycle evidence. There is no automatic
fallback to Windows Codex App Server. Legacy or projection-mismatched mappings
bootstrap a new Thread before Task execution and replace persistence only after
success; an executing Task never silently switches Threads. The only
additional managed-Exec directory permitted for an attachment Task is its exact
Task-local `/tmp/lazy-attachments/<taskid>/` directory; it is never a Project
Data, Workspace, or arbitrary host root.

## `_toLazyAIDeck`

<!-- BEGIN GENERATED CONTROLLER PACKAGE CONTRACT -->
Contract version: `2`
Contract SHA-256 is available from `deck_get_controller_package_contract` and `deck_preflight_controller_package`.

- Filename: `<ProjectID>_<VMR>_toLazyAIDeck_<purpose>.zip`; filename ProjectID must equal `command.json.projectId`.
- Dynamic Project check: filename VMR must equal the registered Project's current VMR. Controller `command.json` has no Base VMR field.
- ZIP top level: only `command.json` and optional `params/`.
- `command.json`: schemaVersion `1`, exact `projectId`, and a non-empty ordered `commands` array.
- Command fields: optional/null `id`, required string `type` and `target`, optional string-array `params`, optional `mode`.
- Omitted/null IDs are completed by Server as `cmd-001`, `cmd-002`, ... without colliding with explicit IDs. Explicit blank or duplicate IDs are invalid.
- Supported types: `scripts-run`, `deck-command`. Generic `powershell` packages are unsupported; publish a bounded Catalog Script or use an advertised `deck-command`.
- `scripts-run`: target is a current public Script Catalog `.bat`; optional mode is `wait`; installed Catalog membership is runtime-advertised.
- Official `git-update.bat` accepts no params; `github-release-publish.bat` accepts one bounded v-prefixed semantic version; `github-workflow-dispatch.bat` accepts an allowlisted workflow key, version, and only declared `name=value` inputs.
- `deck-command`: params must be empty and mode omitted. Current exact static targets: `migrate-project-data`, `validate-revision`, `lazy-blueprint`.
- `validate-revision` additionally requires the completed Revision Journal for the exact current package VMR before execution.
- Apply Patch remains `scripts-run` target `apply-patch.bat` with empty params and payload under `params/apply-patch/`: `manifest.json`, optional `Source/`, plus `ProjectData/` for current Projects or `Workspace/` only for explicitly legacy operational roots.
- Preflight accepts only package metadata/text, performs no execution or mutation, and uses the same normalizer/validator as intake.
- Before user handoff, call `deck_preflight_controller_package` with the intended filename, exact command JSON, every logical ZIP member path plus SHA-256 when available, and Apply Patch manifest text when applicable; proceed only when `succeeded` is true and use its normalized command IDs.
<!-- END GENERATED CONTROLLER PACKAGE CONTRACT -->

The Server reserves root `result.md` for the terminal archive and rejects it in
incoming ZIPs. Commands stop at the first failure.

The managed intake is the exact current Deck root
`<DataRoot>\Downloads\<DeckName>\Inbox`. Server resolves ProjectID to current Deck
membership and owns Inbox, Processing, Completed, Failed, Orphaned, and
TerminalMetadata beneath that Deck; a package in the wrong Deck fails closed.
App/Chat must not route new work through the retired flat `Downloads\Inbox`.
Completed/Failed ZIPs and metadata are disposable transport/cache evidence: the
durable bounded result/Preview/drag source is the retained TaskCard `result.md`,
with Revision Journals retaining Revision evidence. Failed-package Explorer
selection therefore exists only while the matching cache archive still exists.

両方のroutable package discriminatorで、exact wire filenameの重複保護はlifecycle-awareである。
active Processingまたは保持中のCompleted Task Cardは再実行せずrejectし、Downloads ZIPの保持は不要である。保持済みFailed/Interrupted/Orphaned executionは新しいattemptとして再発行できる。
各accepted Controller executionにはServer-owned attempt identityが割り当てられ、retryは新しいTask Card/resultになる。
以前のTask Card/resultはbounded historyとして残り、latest resultはterminal attempt時刻で選択される。Completed/Failed archiveとTerminalMetadataは最大5件のterminal Task Cardが表すVMR windowに追従してbest-effort削除される。
新しく作成する同目的Chat packageは通常どおりflat suffixを進める（例: `R003_1` -> `R003_2`）。

When a valid routable `_toCodex` or `_toLazyAIDeck` package has an exact terminal
archive below the Server Failed root, its matching Project Client may request
one-shot Explorer selection using ProjectID, original filename, and Failed archive
identity. The Server revalidates that exact direct child; selection never opens,
executes, retries, or mutates the ZIP. Cache cleanup can make that selection
unavailable without affecting the Task result. A valid duplicate download fails
as `duplicate_package` without executing its second copy and follows the same rule.

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
no Apply Patch payload handoff. Every public Script receives a Server-owned
`LAZY_AI_DECK_*` context containing exact ProjectID, registered SourcePath,
selected operational root/kind, current VMR, and DeckName; inherited/caller values
in that reserved namespace are cleared first. The Script validates strict membership/hashes and applies
only below those registered roots with bounded rollback. `apply-local` is not a
current Pack capability.

### Official Git/GitHub Scripts

The Starter Pack publishes `git-update.bat`, `github-release-publish.bat`, and
`github-workflow-dispatch.bat` as fixed public capabilities. Git Update has no
public parameters and checkpoints only the current accepted Revision Journal
delta before a normal `origin/main` push. It discovers only safe current
`V*/M*/R*/revision.json` entries plus legacy `Revisions/**/revision.json`, rejects
ambiguous current evidence, and returns deterministic fresh/idempotent commit,
remote, committed-path, retained-dirty-path, and log-reference fields. The GitHub Scripts accept only bounded
version/workflow selectors and manifest-declared inputs. They resolve the
repository from registered Source `origin` and read the Project-authored fixed
allowlist `.lazy-ai-deck/github-operations.json`; the Pack installs only its
schema/example. Generic PowerShell, arbitrary executable/path/repository/URL,
shell fragment, command, refspec, and workflow passthrough remain unsupported.
GitHub authentication is owned by the existing `gh` credential store and is
never accepted through package parameters, manifest data, or logs.

### WSL projection diagnostic

`_toLazyAIDeck` → `scripts-run` → `wsl-projection-diagnostic.bat` is a published
current Starter Pack Catalog capability. Use `params: []` and optional `mode: wait`.
The Server binds inspection to the package ProjectID and its current Deck/Windows
Source/distribution. Bounded read-only WSL processes verify backing visibility,
organized mount equality and the restart entry; no caller path, distribution,
credential or shell input is accepted. It creates only the usual disposable Script
log and never provisions a mount or bootstraps a Thread.

The projection diagnostic reports registered Deck/Project/Windows Source identity,
distribution, backing and expected projection paths, existence and mount evidence,
exact fstab persistence, projection Git top-level/HEAD, and legacy `/source` state
in stdout and its normal `Scripts/Outputs` log. It is read-only, has no public
parameters, and receives identity only from the Server.
