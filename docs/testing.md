# Testing

The stable entry is `Scripts/Test-All.ps1` (PowerShell 7). Node.js 22+ and the .NET 10
SDK are required. No historical Revision numbers are needed to run validation.

```powershell
.\Scripts\Test-All.ps1 -Clean
.\Scripts\Test-All.ps1 -ChromeExecutable $chromeForTesting
.\Scripts\Test-All.ps1 -ChromeExecutable $chromeForTesting -Gui
```

The first command cleans, restores, builds and runs deterministic checks. Supplying
a Chrome for Testing executable enables complete real acceptance. -Gui supplies
isolated dynamic pages and runs the actual SampleCaller for operator acceptance.
Each invocation owns one flat timestamped log under Scripts/Outputs and returns a
nonzero exit code on a failing child. Those disposable logs are ignored.

## Layers
- Core.Tests: 121 checks: original 96 session/HTTP, geometry/native simulation and
  monitor checks plus 25 download transport/lifecycle/bounds/disposal checks.
  It also hosts the isolated real-browser driver. Product operations in that driver
  call the public façade; internal access is limited to deterministic fixtures and
  explicit test evidence such as topology/profile/process/shutdown measurements.
- PublicApi.Tests: 19 checks in an external consumer assembly without friend access. It verifies
  the façade, defaults, encoded read-only frames, invalid placement, async disposal,
  hidden coordinators and absence of WinForms/WPF references in Core.
- Extension.Tests: 33 tests: original 17 binding/worker, cancellation, stale frame,
  target ownership, in-flight tab closure and four concurrent monitor scenarios, plus
  16 download outbox, ordering, exact metadata, retry, fan-out, recovery and bounds cases.
- AuditSource: current tracked/untracked file and directory names, old-name text
  against the explicit governance/history allowlist, dependency/permission/command
  boundaries and generated/private-file patterns. It emits machine-readable JSON.
- BrowserAcceptance/GeometryAcceptance: launch, same-URL sessions, navigation, close,
  worker recovery, profile restore, real negative coordinates, exact offscreen PARK.
- MonitorAcceptance: retained capture/navigation/lifecycle/performance regressions,
  adapted to accepted PARKED-only semantics.
- MultiSessionAcceptance: one Visible ACTIVE plus four PARKED streams, separate
  native identities/colors/fresh hashes, navigation, worker restart, Restore/re-PARK,
  active tab/close, manual Set, close/relaunch and shutdown.
- DownloadAcceptance: neutral local ZIP attachments by direct navigation, five bound
  sessions producing one stream, server-aborted download with official interrupt reason,
  worker stop/alarm recovery during a slow download, and consumer stable/exclusive checks
  and move only after Complete. Final filename availability before Complete is recorded,
  never used as completion authority. Exact Chrome ID/path and no late Interrupted are checked.

Download fixtures use the public Core event/snapshot API. Test-only CDP configures an
isolated download directory and evaluates official extension APIs in the extension
worker; it does not inspect webpage DOM. Production has no Runtime.evaluate call.
Only the consumer test driver performs file checks/move. Neutral test paths may appear
in ignored test evidence; production never logs download paths. Source auditing lists
every production download call and permits only onCreated/onChanged/search.

The native-size comparisons remain test-only coverage; they never enable production
shrinking. Color/hash checks are confined to test tooling, not product image analysis.
All real fixtures use isolated local pages, browser profile and geometry storage.
Browser startup uses the supplied executable without sandbox/notice suppression.

## Public-release gates
Test-All also generates the ignored extension ZIP through the Source-owned internal
packaging helper, checks repeated archive SHA256, and runs AuditPublicRelease. The
standalone Scripts/Package-Extension.ps1 wrapper uses the same helper and its own
flat log. The ZIP has fixed ordering/timestamps, canonical UTF-8/LF and no compression;
an independent ZIP reader checks actual entries, CRC and equality to extension Source.

The public audit covers tracked and untracked candidate files, relative Markdown
file/heading links, known secret/email/personal-path patterns, account identity
allowlisting and binary/image hygiene. The one reviewed PNG is tied to its exact
hash, dimensions and permitted metadata chunks. A changed image requires another
manual privacy review. No OCR or page inspection is added to the product.
See [release preparation](github-release.md) for history-email and publication limits.
Static scans are bounded; public GitHub rendering is checked after actual publication,
not claimed by local candidate validation.

## GUI checklist
Launch five distinct local fixtures. Start monitoring while Visible and verify ACTIVE
tiles without capture. Park four, verify four changing images and a separate ACTIVE
tile. Change table selection; capture targets must not change. Restore one, confirm
only its tile becomes ACTIVE, then re-PARK it. Stop and verify counts hold; Start and
verify all eligible streams resume. Resize the sample and verify aspect-preserving
Zoom/wrapping. Close normally while monitoring and verify test-process cleanup.

The GUI helper reports local fixture URLs and waits for normal sample close. Dynamic
paths /dynamic-a through /dynamic-e provide different fixture colors. An optional
test-only LCWB_TEST_WINDOW_POSITION environment value (`x,y`, physical screen placement)
selects a preferred monitor for isolated launches and relevant acceptance rectangles.
It does not alter product defaults; geometry regressions still exercise multiple
monitors deliberately.

## Evidence limits
Three physical 96-DPI monitors provide real negative-coordinate/offscreen evidence.
Mixed-DPI/removal/topology edge cases also use deterministic fake native geometry.
Normal installed-Chrome consent UI is not equivalent to isolated CfT automation.
Performance reports effective fps, changing hashes, maximum useful gap, capture
latency, Base64 bytes/s and owned caller/Chrome tree CPU and memory. CPU is a percentage
of one core; Base64 excludes JSON/WebSocket headers. Short tests include sampling and
startup overhead and impose no invented hard CPU threshold.
