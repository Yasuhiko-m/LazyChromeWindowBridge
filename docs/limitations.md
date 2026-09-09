# Limitations and support boundary

- Windows desktop only. Core relies on native Windows placement and GDI+ imaging;
  there is no Linux/macOS implementation. The sample uses WinForms.
- Chrome 120+ is declared by the extension; real acceptance uses a specific Chrome
  for Testing build. A pass on that build is not proof for every Chrome version.
- Installed Chrome requires the extension to be enabled in the selected profile.
  Its consent/debugging UI is not completely automated by CfT acceptance.
- Debugger permission is broad, its normal notice is retained, and another debugger
  can contend for a target. Cancellation is respected; no permission retry loop.
- PARK means offscreen placement, not minimize. External applications can still
  move/close owned windows. Retained HWND/PID/property checks reject stale handles.
- Normal is restored exactly when reachable. Changed monitor topology can require
  a deterministic visible fallback. Real acceptance uses three 96-DPI monitors;
  mixed DPI, removal and topology changes also have deterministic simulated coverage,
  not a complete physical mixed-DPI hardware matrix.
- Monitoring is a low-rate human thumbnail, not remote desktop/video/control.
  Four simultaneous PARKED targets are the reference acceptance case. Loads vary
  with page activity, viewport, Chrome version, startup and machine contention.
- Short neutral-page samples are not long-run leak, worst-case video bandwidth or
  background-throttling guarantees. Recorded CPU includes test hash sampling.
- Initial offscreen screenshots can hit the five-second capture budget in CfT.
  The affected session reports Error and detaches while peers retain their own
  state; explicit Start is needed to request a new generation. A passing short run
  does not establish the cause or eliminate this intermittent startup limitation.
- Native PARK shrinking is not used in production. Experiments increased payload
  and did not show a consistent CPU/capture benefit; offscreen viewport reflow could
  lag a native resize. Only captureScreenshot downscaling is adopted.
- MV3 worker restart within a running browser recovers persisted bindings and only
  currently requested PARKED capture. chrome.storage.session is not durable across
  full browser restart; existing live session takeover is not supported.
- Full caller restart loses live session capabilities/bindings. A new launch has a
  new appSessionId; saved launch-URL Normal geometry can be reused.
- Download observation has no tab/window/appSession attribution. It includes the
  connected non-incognito profile's downloads, with one stream per Bridge. One runtime
  accepts one browser-session identity; a new caller cannot take over an old outbox.
- Download retention is bounded; capacity overflow, storage loss, extension reload/update,
  full browser restart and transitions before the first durable write are outside
  delivery guarantees. No unobserved history is imported. See [exact limits](downloads.md).
- Download callbacks are synchronous and exception-isolated, but cannot make blocking
  consumer code safe. Queue slow work and handle asynchronous failures in the consumer.
  Chrome Complete does not replace consumer filesystem validation or eliminate a
  filesystem race between exclusive-open checks and a later move.
- Normal DisposeAsync restores PARKED windows and releases debugger transports.
  Forced process kill, OS crash or power loss cannot guarantee restoration/detachment.
  A hung peer can exceed graceful shutdown bounds; callers should still await disposal.
- The pre-release product rename starts a new geometry directory. Legacy pre-release
  data is not a supported migration contract; no migration subsystem is provided.
- No installer, updater, NuGet package, Web Store publication or GitHub workflow is
  established. Licensing for an eventual public release is not finalized.
