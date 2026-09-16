# Testing

The 0.3.1 release-preparation tree uses Scripts/Test-NuGet.ps1 for package/symbol metadata
and isolated PackageReference checks, and
Scripts/Package-Windows.ps1 for Release/win-x64 self-contained packaging. To exercise
the fresh extracted EXE through the existing -Gui fixture, set LCWB_TEST_SAMPLE_EXECUTABLE
to the inventory.json freshExecutable path. The default still runs the Debug sample.
The real fixture loads the audited Extension 0.3.1 validation ZIP. No publication is performed.

The stable entry is `Scripts/Test-All.ps1` (PowerShell 7). Node.js 22+ and a stable
.NET 10.0.4xx SDK (10.0.401 floor with global.json latestPatch roll-forward) are required.
No historical Revision numbers are needed to run validation.

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
- Core.Tests: session/HTTP, geometry/native simulation, continuous-monitor identity/
  placement/options/latest-frame checks, both capture modes, exact-identity rejection,
  bounded native JPEGs, mode generations, taskbar bookkeeping/restoration and download
  transport/lifecycle/bounds/disposal.
  It also hosts the isolated real-browser driver. Product operations in that driver
  call the public façade; internal access is limited to deterministic fixtures and
  explicit test evidence such as topology/profile/process/shutdown measurements.
- PublicApi.Tests: external consumer assembly without friend access. It verifies
  the façade, defaults, encoded read-only frames, invalid placement, async disposal,
  hidden coordinators and absence of WinForms/WPF references in Core.
- Extension.Tests: binding/worker, cancellation, stale frame, owned tab/in-flight closure,
  five concurrent JPEG streams, same-generation pump wake/options, 1/30fps acceptance,
  0/31fps rejection, no-upscale/quality/command bounds, restart-control socket reuse,
  plus existing download outbox, ordering, retry, fan-out, recovery and bounds cases.
- AuditSource: current tracked/untracked file and directory names, old-name text
  against the explicit governance/history allowlist, dependency/permission/command
  boundaries and generated/private-file patterns. It emits machine-readable JSON.
- BrowserAcceptance/GeometryAcceptance: launch, same-URL sessions, navigation, close,
  worker recovery, profile restore, real negative coordinates, exact offscreen PARK.
- MonitorAcceptance: Visible JPEG, repeated placement and option continuity, exact
  monitor generation/socket identity and debugger attach/detach counters, output bounds,
  unchanged native bounds/viewport, single-session30fps measurement, lifecycle/regressions.
- NativeWindowAcceptance: exact-HWND WGC composition with a measurable non-client extent,
  no additional CDP screenshot commands, one/five-session performance, mixed Visible/
  Parked streams, pause/frozen/peer/resume, mode generation, taskbar isolation/restoration,
  global Stop/Start and zero-resource shutdown. Its deterministic pages are local.
- MultiSessionAcceptance: five JPEG streams with mixed Visible/Parked placement, separate
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
Browser startup uses the supplied executable and production ChromeLauncher argument
builder, including --silent-debugger-extension-api; test-only startup additionally
loads the isolated extension and local CDP endpoint. No sandbox suppression is added.
The fixture reads only its own OS process command line and records the silent flag
count, PID and required-flag booleans, never full command lines or bootstrap tokens.
Notice absence is a GUI observation, not a permanent Chrome UI-text assertion.

SessionMonitorAcceptance runs inside the existing five-session fixture. It verifies
target-only detach, frozen JPEG SHA256/Sequence/ReceivedAt, continued peer traffic and
unchanged peer generations/sockets/attachments, no Park/Restore auto-toggle, paused
global option update, same-socket resume, global clear/batch restart and final cleanup.
Deterministic tests also cover rejected unknown/closed/stale/stopped calls, rapid
OFF/ON during acquisition, late-frame rejection and explicit error retry.

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

For a task starting with explicitly excluded inherited dirt, LCWB_TASK_BASELINE may
name a Source-local JSON file recording startHead and inherited path/sha256 pairs.
Audits still list inherited findings but exclude them from task violations only when
HEAD and every inherited byte hash remain unchanged. Without this input the existing
full candidate audit applies. This does not stage, ignore, delete or normalize any file.

## GUI checklist
Launch five distinct local fixtures. Start monitoring while Visible and verify five
fresh JPEG previews. Park four; all five previews continue. Change table selection;
capture targets must not change. Restore one, then Park/Restore/Park: latest is never
cleared merely for placement and the monitor generation stays fixed. Change FPS,
JPEG width/height and Apply preview while monitoring; verify subsequent frame sizes
and unchanged generation/native bounds. Stop and verify previews clear/counts hold; Start and
verify all eligible streams resume. Resize the sample and verify aspect-preserving
Zoom/wrapping. Close normally while monitoring and verify test-process cleanup.
Also pause only one selected session: its last JPEG/frame number remains with a static
frozen label while peers continue. Park/Restore and Apply options must not resume it.
Resume it, then global Stop/Start; verify target-only fresh frames then batch restart.
Observe Visible Chrome for the debugger infobar on the tested build without assuming
the flag changes permission or guarantees suppression on other versions.
Repeat with NativeWindow selected: verify the title-bar/window composition is present,
PARKED sessions remain live, no picker appears, and debugger audit counters do not grow.
Hide/show one selected taskbar entry while another remains normal; PARK/RESTORE and
Pause/Resume must not change that policy. Confirm normal sample close restores it.

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
NativeWindow additionally requires Windows 10 1903+ and WGC/D3D11 availability; an
explicit unsupported-mode result is not a BrowserViewport failure or fallback.
Automated taskbar acceptance establishes exact-HWND extended-style transitions,
successful native refresh calls, peer isolation and disposal restoration. Final
Explorer button pixels require the separate visual GUI checklist and are not claimed
by deterministic query evidence.
