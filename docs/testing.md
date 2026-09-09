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
- Core.Tests: original 96 session/HTTP, geometry/native simulation and monitor checks.
  It also hosts the isolated real-browser driver. Product operations in that driver
  call the public façade; internal access is limited to deterministic fixtures and
  explicit test evidence such as topology/profile/process/shutdown measurements.
- PublicApi.Tests: an external consumer assembly without friend access. It verifies
  the façade, defaults, encoded read-only frames, invalid placement, async disposal,
  hidden coordinators and absence of WinForms/WPF references in Core.
- Extension.Tests: binding/worker, cancellation, stale frame, target ownership,
  in-flight tab closure and four concurrent monitor scenarios.
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

The native-size comparisons remain test-only coverage; they never enable production
shrinking. Color/hash checks are confined to test tooling, not product image analysis.
All real fixtures use isolated local pages, browser profile and geometry storage.
Browser startup uses the supplied executable without sandbox/notice suppression.

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
