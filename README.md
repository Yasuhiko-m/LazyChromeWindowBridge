# LazyChromeWindowBridge

**Control the Chrome window, not the webpage.**

A Windows-to-Chrome bridge for deterministic session/window ownership, native geometry,
full offscreen PARK/RESTORE, lightweight human monitoring, manual window control and
Chrome Download Manager lifecycle events — without DOM automation.

![SampleCaller with five bound sessions: one ACTIVE and four PARKED/LIVE neutral thumbnails](docs/images/monitor-overview.png)

*Actual SampleCaller with isolated local color fixtures. ACTIVE has no capture;
four PARKED windows supply independent human-view thumbnails.*

Built for .NET Windows applications that need Chrome window management and browser
integration through a Manifest V3 extension. Unlike DOM-oriented browser automation
tools, it works with Chrome application/window state and exact native ownership.
Licensed under [MIT](LICENSE). This is an early release, with explicit [support limits](docs/limitations.md).

## What it does
- Launch independent Chrome windows, including several with the same launch URL.
- Retain deterministic appSessionId ↔ Chrome WindowId ↔ native HWND/PID/property identity.
- Keep Chrome session binding independent of navigation and active-tab changes.
- Persist Chrome window position and size by the original launch URL; set manual bounds.
- PARK a window fully outside all monitors and RESTORE its protected Normal bounds.
- Read state/bounds and set a Visible window's physical-pixel placement.
- Monitor all eligible PARKED sessions independently; Visible windows show ACTIVE.
- Observe profile-global Created / Complete / Interrupted downloads once per Bridge.

## What it intentionally does not do
- DOM scraping or Runtime.evaluate.
- OCR, semantic image analysis or page-content analysis.
- Automated click/input or remote webpage control.
- ChatGPT output/completion extraction or other webpage completion detection.
- Network response inspection, session recording or cloud relay.

Chrome download lifecycle is browser metadata, not a webpage completion signal.
The monitor is a small human status overview, not remote desktop or video streaming.

## Architecture
```mermaid
flowchart TB
    Consumer[Windows Consumer] <-->|public Core API| Core[LazyChromeWindowBridge.Core]
    Core <-->|authenticated 127.0.0.1 HTTP / WebSocket| Extension[LazyChromeWindowBridge.Extension]
    Extension <-->|Chrome APIs| Chrome[Chrome]
```

This Chrome extension native bridge uses the existing loopback listener rather than a
native-messaging host installation. Core owns the Win32 Chrome window mapping;
the consumer owns presentation and filesystem decisions.

| Component | Responsibility |
| --- | --- |
| `src/LazyChromeWindowBridge.Core` | Public `BridgeRuntime` API, ownership, loopback transport, native geometry and encoded thumbnails |
| `src/LazyChromeWindowBridge.Extension` | MV3 binding/recovery, debugger viewport capture and read-only download observation |
| `samples/LazyChromeWindowBridge.SampleCaller` | WinForms example using only the public Core API |
| `tests/` | Core, external public-API consumer and extension checks |

Core has no WinForms/WPF UI type dependency. Its bounded JPEG decoding uses the
built-in Windows Desktop GDI+ imaging runtime. SampleCaller owns PictureBox/Zoom.

## Requirements
- Windows desktop session supported by .NET 10, with an interactive display.
- .NET 10 SDK for development (includes the required build/targeting components).
- Runtime deployments need .NET 10, ASP.NET Core and Windows Desktop shared runtimes.
- Google Chrome 120+ with the unpacked extension enabled in the same profile.
- PowerShell 7 and Node.js 22+ for validation.
- Chrome for Testing with extension loading support for isolated browser acceptance.

No third-party NuGet or npm packages are required.

## Quick start
Run from the repository root:

```powershell
dotnet restore .\LazyChromeWindowBridge.sln
dotnet build .\LazyChromeWindowBridge.sln --no-restore
```

In Chrome, open `chrome://extensions`, enable Developer mode, select **Load unpacked**
and choose `src/LazyChromeWindowBridge.Extension`. Review the requested permissions:
storage, alarms, debugger, downloads and IPv4 loopback host access. The broad downloads
permission is used only for official event observation and lookup by download ID.

```powershell
dotnet run --project .\samples\LazyChromeWindowBridge.SampleCaller
```

SampleCaller discovers installed Chrome. To choose an executable/profile explicitly:

```powershell
dotnet run --project .\samples\LazyChromeWindowBridge.SampleCaller -- `
  --chrome-executable "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
```

The optional `--chrome-user-data-dir` and `--geometry-directory` arguments accept
ordinary paths; use a dedicated test profile for acceptance. Do not attach debugger
tooling to a profile containing unrelated personal sessions for these tests.

Enter a launch URL and click Launch. Wait for Bound / Visible, then Start monitor.
Park the desired sessions: each becomes an independent LIVE tile. Restore one and
its tile becomes ACTIVE while its peers continue. Stop monitor stops every capture.
Closing SampleCaller normally restores PARKED windows and leaves Chrome open.

## Integrating Core
Reference `src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj` from
a `net10.0-windows` application. The sample is not a library dependency.

```csharp
using LazyChromeWindowBridge.Core;

await using var bridge = await BridgeRuntime.StartAsync(BridgeOptions.Parse([]));
var session = await bridge.LaunchAsync("https://example.com/");

// In your application's UI/timer, wait until GetWindow(id) reports Visible.
var window = bridge.GetWindow(session.AppSessionId);
if (window?.State == PlacementState.Visible)
{
    bridge.StartMonitoring();             // Global; only PARKED sessions qualify.
    bridge.Park(session.AppSessionId);
    // On later UI ticks: bridge.GetLatestFrame(id)?.Jpeg supplies encoded bytes.
    bridge.Restore(session.AppSessionId);  // Same owned window, protected Normal.
}
```

Binding is asynchronous. See [Integration](docs/integration.md) for a complete bounded
wait, manual bounds, frame consumption and shutdown. A missing extension is reported
as a session failure, not a different target selection.

## PARK and monitoring
For Chrome multi-window workloads, PARK is explicit logical state and native placement outside every monitor; it is not
minimization. Normal is protected while PARKED. Manual SetWindowBounds requires a
Visible session; Restore first if it is PARKED. Coordinates are physical pixels,
including reachable negative-coordinate monitors.
This Windows Chrome integration supports multi-monitor layouts and negative coordinates.
An offscreen Chrome window retains its full native size: PARK/RESTORE browser window
management does not minimize it or use experimental native shrinking.

Default monitoring is **2 fps, maximum 240×135, JPEG quality 70**, with preserved aspect
ratio and no capture upscale. Four simultaneous PARKED streams are the validated
reference workload. Native shrinking was measured and rejected; the native window
keeps its normal size while only the captured image is scaled.

Chrome's debugger permission is broad, although this implementation only uses
viewport metrics and JPEG screenshot commands. The normal debugging notice remains.
Cancellation is respected; explicit Start is required to retry a canceled monitor.
Chrome window thumbnail monitoring is PARKED-only; Chrome window control remains
limited to the owned native window and explicit consumer commands.

## Download lifecycle
The Chrome downloads API supplies this read-only Chrome download lifecycle stream.
Subscribe to `BridgeRuntime.DownloadChanged` before launching sessions. Events contain
`DownloadId`, `State`, exact Chrome `Filename`, optional `Error` and `ObservedAt`.
There is no appSessionId or URL attribution: downloads belong to the Chrome profile.
Five bound sessions in one Bridge still receive one lifecycle stream. `GetDownloads()`
and `GetDownload(id)` expose bounded current runtime snapshots.

Chrome Complete is the completion authority. The consumer must validate the expected
directory/name, existence, stable size/time and exclusive access before moving a file.
Core never opens or moves it. Handlers run synchronously and must return promptly;
queue filesystem/UI work in the consumer. Absolute filenames are sensitive: do not
log them. See [Download lifecycle](docs/downloads.md) for recovery and capacity limits.

## Validation
```powershell
# Clean build + deterministic Core/public API/extension checks.
.\Scripts\Test-All.ps1 -Clean

# Complete real browser/native/five-session acceptance (set your CfT path).
.\Scripts\Test-All.ps1 -ChromeExecutable $chromeForTesting

# Interactive sample fixture with isolated local pages.
.\Scripts\Test-All.ps1 -ChromeExecutable $chromeForTesting -Gui
```

Each invocation writes one disposable log under `Scripts/Outputs`. Without
`-ChromeExecutable`, the entry explicitly reports that real browser tests were skipped.
See [Testing](docs/testing.md) for coverage and evidence boundaries.

## Further reading
- [Architecture](docs/architecture.md)
- [Integration/API](docs/integration.md)
- [Download lifecycle](docs/downloads.md)
- [Security and privacy](docs/security.md)
- [Limitations](docs/limitations.md)
- [Testing](docs/testing.md)
- [Product and governance identity](docs/identity.md)
- [First-release notes](docs/releases/v0.1.0.md)
- [Release packaging and maintainer steps](docs/github-release.md)

## Development

Lazy AI Deck was used as the development orchestration environment.
LazyChromeWindowBridge itself has no runtime dependency on Lazy AI Deck.

Lazy AI Deck is a consumer of this independent product, not its runtime container.
Pre-release geometry data from the former product is not a supported migration
contract; see the identity and limitations notes.
