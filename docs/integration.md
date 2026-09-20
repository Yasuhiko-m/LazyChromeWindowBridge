# Integration with the public Core API

This describes the published GitHub v0.3.2 Source/API. Use matching Core and
extension versions; mixed-version compatibility is not guaranteed. CWS may lag or skip
versions, so use the matching GitHub Release extension ZIP with Developer mode / Load
unpacked when an exact Store version is unavailable.
v0.3.2 adds `SendKeyChordAsync(appSessionId, new BrowserKeyChord(...))` for exactly one allowlisted key/chord to the owned active tab. Completion confirms `Input.dispatchKeyEvent` completed, not a page or browser-UI effect; no macro, text input, JavaScript, DOM inspection, click automation, or post-input inspection is provided.
PARK/RESTORE controls native window placement only and does not
automatically start, stop, pause or resume monitoring. Monitoring policy belongs to
the Caller, who explicitly chooses it with `SetSessionMonitoring(appSessionId, enabled)`.

Target `net10.0-windows` for BrowserViewport compatibility, or
`net10.0-windows10.0.18362.0` or later for NativeWindow, and reference the Core project. Start one `BridgeRuntime`
per consuming application's intended lifetime. SampleCaller demonstrates this directly:
its project references Core, has no InternalsVisibleTo grant and uses no coordinator
or native implementation type.

`BridgeOptions.Parse(args)` is the shared optional CLI/discovery helper. Alternatively
construct `BridgeOptions` with an explicit Chrome executable, optional user-data path
and optional geometry directory. The extension must be enabled in that Chrome profile.

## Chrome launch options

Existing `new BridgeOptions(executable, userDataDirectory, geometryDirectory)` construction
remains valid. LCWB adds `--disable-backgrounding-occluded-windows` once by default to
help Chrome keep an owned window rendering when LCWB PARKs it fully offscreen. Set
`PreserveBackgroundRendering` to `false` to omit only that LCWB policy switch.

`AdditionalChromeArguments` accepts ordered Chrome switches. Each is added as one
`ProcessStartInfo.ArgumentList` entry after LCWB's profile/notice/preservation flags and
before `--new-window`; the bootstrap URL remains final. For example:

```csharp
var options = new BridgeOptions(chromeExecutable, chromeProfileDirectory)
{
    PreserveBackgroundRendering = false,
    AdditionalChromeArguments = ["--load-extension=C:\\cft-extension"]
};
```

`--load-extension=<directory>` can be useful with Chrome for Testing or Chromium; this
does not promise that branded Chrome accepts it. Arguments must be non-empty switches
without control characters. LCWB rejects case-insensitive attempts to provide
`--user-data-dir`, `--new-window`, `--no-first-run`, `--no-default-browser-check`,
`--silent-debugger-extension-api`, or `--disable-backgrounding-occluded-windows`, with
or without `=value`. This preserves LCWB ownership and bootstrap invariants.

CLI users can repeat `--chrome-argument <switch>`:

```powershell
dotnet run --project .\samples\LazyChromeWindowBridge.SampleCaller -- `
  --chrome-argument "--load-extension=C:\\cft-extension"
```

## Launch and wait for binding
```csharp
using LazyChromeWindowBridge.Core;

await using var bridge = await BridgeRuntime.StartAsync(BridgeOptions.Parse([]));
var session = await bridge.LaunchAsync("https://example.com/");
var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
WindowSnapshot? window;
while ((window = bridge.GetWindow(session.AppSessionId))?.State != PlacementState.Visible)
{
    var state = bridge.GetSession(session.AppSessionId);
    if (state is null || state.State is SessionState.Failed or SessionState.Closed)
        throw new InvalidOperationException(state?.Detail ?? "Session unavailable.");
    if (DateTimeOffset.UtcNow >= deadline)
        throw new TimeoutException("Window did not become ready.");
    await Task.Delay(200);
}

// Current/Normal are physical pixels. Choose a reachable position on your displays.
var bounds = window.Current!;
bridge.SetWindowBounds(session.AppSessionId, bounds);
bridge.StartMonitoring(new CaptureOptions(2, 240, 135, CaptureMode.NativeWindow));
bridge.SetShowInTaskbar(session.AppSessionId, false); // Exact HWND; independent of capture/placement.
bridge.Park(session.AppSessionId);

// Poll on subsequent UI/timer ticks; a frame may initially be null.
MonitorFrame? frame = bridge.GetLatestFrame(session.AppSessionId);
if (frame is not null)
{
    ReadOnlyMemory<byte> jpeg = frame.Jpeg;
    // Decode/display in your UI. Keep session/identity/generation metadata attached.
}

bridge.Restore(session.AppSessionId);
bridge.SetShowInTaskbar(session.AppSessionId, true);
bridge.StopMonitoring();
// await using performs normal async shutdown.
```

The loop is application-side readiness handling, not a page-content decision or
product retry framework. Do not block a UI thread while waiting for launch/disposal.

## Contract
| Public method | Result/semantics |
| --- | --- |
| StartAsync(options) | Creates a loopback runtime |
| LaunchAsync(url) | Creates a session; binding/native mapping completes asynchronously |
| GetSessions(), GetSession(id) | Immutable session snapshots; navigation leaves ownership intact |
| GetWindow(id) | WindowSnapshot or null while unmapped |
| SetWindowBounds(id, bounds) | Exact live Visible window only; physical pixels, checked native result |
| Park(id), Restore(id) | Explicit placement transitions on the exact owned window |
| SetShowInTaskbar(id, show) | Exact-owned-HWND taskbar policy; independent, idempotent and restored on disposal |
| StartMonitoring(options?), StopMonitoring() | Global eligible Visible/Parked JPEG requests; defaults BrowserViewport at 2fps/240×135; repeated same-mode Start updates options in place |
| GetMonitorState() | Aggregate and per-session monitoring status/counters |
| SetSessionMonitoring(id, enabled) | Pause/resume one live owned session; requires global Start; placement never changes this policy |
| GetLatestFrame(id) | Latest encoded frame/metadata, including a frozen Paused frame, or null |
| DisposeAsync() | Idempotent normal shutdown; await it before exiting |

WindowSnapshot exposes AppSessionId, WindowId, LaunchUrl, NativeIdentity, State,
Current, Normal, Dpi, Error and PlacementGeneration. Dead/stale native identities
report Closed with no current rectangle; no replacement window is discovered.

Set requires valid dimensions and reachable title-bar placement. Ordinary negative
coordinates are valid on negative-coordinate monitors. PARKED/transition state
requires Restore; Closed/unmapped/stale state fails. Native checks use a bounded
2px tolerance. The existing observer persists stable Visible placement, so immediately
after Set, Normal can still show its prior value until the debounce completes.

MonitorSnapshot includes Enabled, Frames, Bytes, Connections, CapturingConnections
and per-session Waiting/Live/Error/Disconnected states. Obtain Visible/Parked placement
separately from GetWindow().State. LastFrameAt, dimensions,
age derived by your UI and Error can inform presentation. Frame.Sequence is cumulative;
use (Generation, Sequence) for replacement. Discard a displayed image when latest
becomes null, and never identify the session by pixels or its current page URL.

`GetLatestFrame` and `GetMonitorState` are observational cached reads and may be polled
frequently (including preview repaint cadence) without initiating geometry probes,
generation changes, worker cancellation, or frame clearing. A transient inability to
read current native bounds likewise does not invalidate an otherwise Bound, mapped,
exact-owned Visible/Parked monitor; confirmed closed placement/session state or exact
NativeIdentity loss still fails closed.

`CaptureOptions.Mode` defaults to `CaptureMode.BrowserViewport`, so existing calls such
as `new CaptureOptions(2, 240, 135)` keep the 0.2.0 behavior. NativeWindow captures the
exact `NativeIdentity.Hwnd`; it does not attach Chrome debugger for capture and never
falls back to BrowserViewport. Its `MonitorFrame.Mode` is NativeWindow and `TabId` is
the documented `-1` not-applicable sentinel. A mode change advances each enabled
target generation and clears its live latest frame so late frames from the prior
provider cannot publish. Paused previews remain frozen until caller-controlled resume.

Park/Restore and placement-generation changes keep the same monitor generation and
latest JPEG. BrowserViewport also keeps its WebSocket and same-tab debugger. To update a running preview, call
`bridge.StartMonitoring(new CaptureOptions(15, 640, 360))`. The next practical capture
uses the new options without a restart; an in-flight frame may use prior settings.
FPS accepts inclusive1–30 (default2), with roughly2–30 recommended. 30 is a requested
ceiling, not measured throughput. MaxWidth/MaxHeight bound only the aspect-preserving
JPEG, without upscale, native resize, viewport resize or zoom change; quality stays70.
An explicit Start after Error is a separate retry generation. NativeWindow uses a
bounded D3D11 GPU resize followed by bounded CPU JPEG encoding at quality70.

For source-aware layouts, call `await bridge.GetCaptureSourceSizeAsync(id, mode)` after
the owned session is mapped and before `StartMonitoring`. It reports the current natural
source only (CSS visual-viewport coordinates for BrowserViewport; WGC native pixels for
NativeWindow) and creates no frame. Set `CaptureOptions.Region` to an immutable zero-based
grid rectangle (`Columns`, `Rows`, `Column`, `Row`, spans); Full is 1×1 and callers may
wrap friendly presets. The selected region is cropped before resize with deterministic
floor boundaries. `CaptureOptions.Resize == null` preserves the legacy bounding/no-upscale
behavior. An explicit `CaptureResize(width,height,filter)` permits exact two-dimension
output (including distortion), preserves aspect for one dimension, and retains natural
cropped size for null/null; explicit resize may upscale within 8192-per-side/24M-pixel
limits. Filters are NearestNeighbor, Bilinear, and Bicubic policy requests, not identical
cross-backend kernels. BrowserViewport captures a natural PNG without CDP clip/scale and
processes region/resize to final JPEG in the extension; NativeWindow does the crop/resize
on its WGC/D3D11 GPU surface before CPU readback. Region changes create a new monitor
generation; resize/filter changes are in-place.

NativeWindow currently accepts Bilinear using the existing D3D11 VideoProcessor’s
driver-native scaling path. That API does not expose nearest-neighbor or bicubic sampler
selection, so explicit NativeWindow NearestNeighbor/Bicubic requests fail before frame
publication; use BrowserViewport for those filters. This avoids silently changing the
caller’s requested resampling policy.

Core returns ReadOnlyMemory<byte> JPEG data; it does not return Bitmap or a UI control.
Decode only when a new frame is available. SampleCaller retains only one displayed
bitmap per tile and disposes superseded images. Consumers must not build page analysis
or output extraction into this human-view-only integration.

## Caller-owned session policy
PARK/RESTORE never automatically switches Monitor ON/OFF. For example:
```csharp
bridge.StartMonitoring(); // Batch ON for all live sessions, including Visible windows.
bridge.SetSessionMonitoring(id, false); // Paused; last JPEG/Sequence/ReceivedAt retained.
bridge.Park(id);                        // Still Paused; no automatic resume.
bridge.Restore(id);                     // Still Paused.
bridge.StartMonitoring(new(15, 640, 360)); // In-place options; id remains Paused.
bridge.SetSessionMonitoring(id, true);  // Same waiting socket; Waiting then fresh Live.
```
Pause advances only the target generation, rejecting late frames, and stops its capture
resource while retaining ownership, geometry and the waiting extension connection.
BrowserViewport detaches its debugger; NativeWindow releases its WGC/D3D resources. Peers continue.
A frozen frame is not Live; inspect State and retain its original timestamp. Resume
clears the frozen frame until a new frame arrives; Sequence then advances. Repeated
OFF/healthy ON is idempotent; ON after Error explicitly retries that session.
Unknown/unbound/closed/stale targets or calls during global Stop throw
InvalidOperationException. Disposal throws ObjectDisposedException. No implicit global Start.
New live sessions default to ON while the global subsystem is running.

LCWB launches Chrome with `--silent-debugger-extension-api` as best-effort BrowserViewport notice
suppression. It does not change debugger permission or the production command allowlist
(Page.getLayoutMetrics/Page.captureScreenshot). NativeWindow uses neither command.
Chrome may ignore the flag, and existing
profile processes may retain their original flags; monitoring still functions.

## Shutdown and restart
StopMonitoring clears latest JPEGs, detaches capture, stops frame traffic and reaches
CapturingConnections=0. Restart-control WebSockets remain; Start reuses them. Only
Dispose/shutdown requires Connections=0.
Global Stop clears frozen Paused images too. The next global Start batch-enables all
live sessions; a Start while already enabled only updates options and preserves pauses.

Await DisposeAsync during normal application close. It restores parked windows and
detaches monitoring; it does not close the browser. There is no live-session takeover
after a full caller restart. A new launch creates a new session and may restore the
saved geometry for its launch URL. See [limitations](limitations.md) for browser
restart, forced termination and data-migration boundaries.
