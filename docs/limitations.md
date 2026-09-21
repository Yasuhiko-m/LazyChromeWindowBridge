# Limitations and support boundary

- Windows desktop only. Core relies on native Windows placement and GDI+ imaging;
  there is no Linux/macOS implementation. The sample uses WinForms.
- NativeWindow additionally requires Windows 10 version 1903/build 18362 or later,
  Windows Graphics Capture support and a functioning Direct3D 11 video processor.
  Unsupported systems fail that mode explicitly; BrowserViewport remains available.
- Chrome 120+ is declared by the extension; real acceptance uses a specific Chrome
  for Testing build. A pass on that build is not proof for every Chrome version.
- Installed Chrome requires the extension to be enabled in the selected profile.
  Its consent/debugging UI is not completely automated by CfT acceptance.
- Debugger permission is broad. LCWB launches use --silent-debugger-extension-api for
  best-effort Chrome-dependent infobar suppression; it does not weaken permission.
  A notice may still appear if Chrome ignores the flag or reuses an existing profile
  process launched without it. Capture does not depend on suppression. Another debugger
  can contend for a target. Cancellation is respected; no permission retry loop.
  Visual infobar absence was not established in acceptance and is not guaranteed.
- PARK means offscreen placement, not minimize. External applications can still
  move/close owned windows. Retained HWND/PID/property checks reject stale handles.
- Normal is restored exactly when reachable. Changed monitor topology can require
  a deterministic visible fallback. Real acceptance uses three 96-DPI monitors;
  mixed DPI, removal and topology changes also have deterministic simulated coverage,
  not a complete physical mixed-DPI hardware matrix.
- Monitoring is a human JPEG preview, not remote desktop/video/control. NativeWindow
  captures the composed owned window and may include title bar/border; it is not a
  client-area-only mode and has no BrowserViewport fallback.
  Requests allow 1–30 fps, with default2 and roughly 2–30 recommended. 30fps is a request
  ceiling, not a throughput guarantee. Five mixed Visible/Parked targets are the
  accepted reference workload. Debugger attachment may exist in either placement. Loads vary
  with page activity, viewport, Chrome version, startup and machine contention.
- Short neutral-page samples are not long-run leak, worst-case video bandwidth or
  background-throttling guarantees. Recorded CPU includes test hash sampling.
- Initial offscreen BrowserViewport screenshots can hit the five-second capture budget in CfT.
  The affected session reports Error and detaches while peers retain their own
  state; explicit Start is needed to request a new generation. A passing short run
  does not establish the cause or eliminate this intermittent startup limitation.
- Native PARK shrinking is not used in production. Experiments increased payload
  and did not show a consistent CPU/capture benefit; offscreen viewport reflow could
  lag a native resize. Only captureScreenshot downscaling is adopted.
- MV3 worker restart within a running browser recovers persisted bindings and only
  currently requested Visible/Parked capture. chrome.storage.session is not durable across
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
- Normal DisposeAsync restores PARKED windows, taskbar styles changed by LCWB, debugger
  transports and WGC/D3D resources.
  Forced process kill, OS crash or power loss cannot guarantee restoration/detachment.
  A hung peer can exceed graceful shutdown bounds; callers should still await disposal.
- The pre-release product rename starts a new geometry directory. Legacy pre-release
  data is not a supported migration contract; no migration subsystem is provided.
- No installer or updater is established. GitHub/NuGet 0.1.0 and 0.2.0 publication are
  historical. CWS 0.2.0 is general-public but does not match Core/Extension 0.3.x.
  GitHub v0.3.2 is published; Store review can lag or skip versions, so use its matching
  GitHub Release extension ZIP while Chrome Web Store 0.3.2 is pending review and not
  public. CWS is not
  a guaranteed archive of every LCWB version. Chrome may ignore its supported
  background-rendering switch; PreserveBackgroundRendering is not a rendering SLA.
- Source 0.3.2 permits one allowlisted `Input.dispatchKeyEvent`
  chord for an exact owned active tab. It is not a macro, text-entry, click, JavaScript,
  DOM, or remote-control API; successful dispatch does not establish page handling or
  browser shortcut behavior.
