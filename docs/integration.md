# Integration with the public Core API

Target `net10.0-windows` and reference the Core project. Start one `BridgeRuntime`
per consuming application's intended lifetime. SampleCaller demonstrates this directly:
its project references Core, has no InternalsVisibleTo grant and uses no coordinator
or native implementation type.

`BridgeOptions.Parse(args)` is the shared optional CLI/discovery helper. Alternatively
construct `BridgeOptions` with an explicit Chrome executable, optional user-data path
and optional geometry directory. The extension must be enabled in that Chrome profile.

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
bridge.StartMonitoring();
bridge.Park(session.AppSessionId);

// Poll on subsequent UI/timer ticks; a frame may initially be null.
MonitorFrame? frame = bridge.GetLatestFrame(session.AppSessionId);
if (frame is not null)
{
    ReadOnlyMemory<byte> jpeg = frame.Jpeg;
    // Decode/display in your UI. Keep session/identity/generation metadata attached.
}

bridge.Restore(session.AppSessionId);
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
| StartMonitoring(options?), StopMonitoring() | Global eligible PARKED requests; defaults 2fps/240×135 |
| GetMonitorState() | Aggregate and per-session monitoring status/counters |
| GetLatestFrame(id) | Latest encoded frame/metadata, or null |
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
and per-session states. ACTIVE means Visible/no capture. LastFrameAt, dimensions,
age derived by your UI and Error can inform presentation. Frame.Sequence is cumulative;
use (Generation, Sequence) for replacement. Discard a displayed image when latest
becomes null, and never identify the session by pixels or its current page URL.

Core returns ReadOnlyMemory<byte> JPEG data; it does not return Bitmap or a UI control.
Decode only when a new frame is available. SampleCaller retains only one displayed
bitmap per tile and disposes superseded images. Consumers must not build page analysis
or output extraction into this human-view-only integration.

## Shutdown and restart
Await DisposeAsync during normal application close. It restores parked windows and
detaches monitoring; it does not close the browser. There is no live-session takeover
after a full caller restart. A new launch creates a new session and may restore the
saved geometry for its launch URL. See [limitations](limitations.md) for browser
restart, forced termination and data-migration boundaries.
