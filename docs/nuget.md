# LazyChromeWindowBridge.Core

Windows-only .NET 10 Core library for session-bound Chrome windows, native geometry,
PARK/RESTORE, human-only thumbnails, exact-HWND native capture, per-window taskbar
policy and profile-global download lifecycle events.

## Install Core and the matching extension

NuGet contains **`LazyChromeWindowBridge.Core` only**. Install Core `0.3.1` from NuGet
and install/use the matching Chrome extension in the Chrome profile that hosts the
owned windows. The `.nupkg` contains no Chrome extension files or ZIP, SampleCaller,
or installer. Matching Core and Extension versions are recommended.

The Chrome Web Store is the normal convenient extension distribution path when the
matching version is available. Store review can delay the newest extension version.
If the Store version does not match Core `0.3.1`, obtain the matching deterministic
extension asset from the GitHub Release instead:

1. Download `LazyChromeWindowBridge.Extension-0.3.1-cws.zip` from GitHub Release
   `v0.3.1`.
2. Extract the ZIP to a dedicated directory.
3. Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**,
   and select that extracted extension directory.

This guidance does not assert that Chrome Web Store 0.3.1 is approved or published.
The submitted CWS 0.2.0 review and historical NuGet 0.1.0/0.2.0 publications are
unaffected.

BrowserViewport consumers can target `net10.0-windows`; NativeWindow consumers target
`net10.0-windows10.0.18362.0`. The package carries the ASP.NET Core and Windows Desktop
framework references; those shared runtimes are required for a framework-dependent
consumer. The Core assembly does not expose WinForms/WPF UI types.

```csharp
using LazyChromeWindowBridge.Core;

await using var bridge = await BridgeRuntime.StartAsync(
    new BridgeOptions(chromeExecutable, chromeProfileDirectory));
var session = await bridge.LaunchAsync("https://example.com/");
// Use GetWindow(session.AppSessionId) from your UI/timer to observe binding.
// Monitoring and native placement remain explicit consumer operations.
```

After the session is Bound with a live native window, these are separate caller actions:

```csharp
bridge.StartMonitoring(); // All live Visible/Parked sessions; default 2 fps, 240x135.
bridge.SetSessionMonitoring(session.AppSessionId, false); // Frozen Paused JPEG.
bridge.Park(session.AppSessionId); // Placement only; still Paused.
bridge.Restore(session.AppSessionId); // Placement only; still Paused.
bridge.SetSessionMonitoring(session.AppSessionId, true); // Fresh capture, same waiting socket.
bridge.StartMonitoring(new CaptureOptions(5, 640, 360)); // In-place options update.
bridge.StartMonitoring(new CaptureOptions(2, 240, 135, CaptureMode.NativeWindow));
bridge.SetShowInTaskbar(session.AppSessionId, false); // Exact HWND; independent policy.
bridge.SetShowInTaskbar(session.AppSessionId, true);
bridge.StopMonitoring(); // Batch stop; clears all JPEGs, retains restart sockets.
```

LCWB launches add `--disable-backgrounding-occluded-windows` once by default so a
fully offscreen PARKed owned window can continue rendering where Chrome supports it.
Set `new BridgeOptions(executable, profile) { PreserveBackgroundRendering = false }`
to omit only that policy switch. Add ordered caller switches with
`AdditionalChromeArguments`; `--chrome-argument <switch>` is the matching repeatable
CLI form. `--load-extension=<directory>` is allowed for Chrome for Testing/Chromium,
with no promise for branded Chrome. LCWB rejects caller overrides of its user-data,
new-window, first-run, browser-check, debugger-notice and background-rendering flags.

Monitoring policy belongs to the Caller. PARK/RESTORE never automatically starts,
stops, pauses or resumes monitoring. Visible and Parked live owned windows use the
same human-view JPEG path. Pause keeps the exact last frame/Sequence/ReceivedAt in
memory as Paused, not Live; peers continue. Resume clears that frozen preview and
obtains a fresh frame on the waiting control connection. Start while already enabled
updates options without resuming explicit pauses; Start after Stop enables all live
sessions again. Dispose closes retained connections.

FPS requests accept 1–30 inclusive, default 2; 30 is a ceiling, not a throughput SLA.
Default bounds 240x135, aspect preservation, no upscale and JPEG quality 70 remain.
Options affect JPEG output only, never native size, viewport or zoom. Placement and
ordinary same-mode options updates preserve the acquisition generation. A mode change
advances only that target generation. BrowserViewport debugger commands are exactly
Page.getLayoutMetrics and Page.captureScreenshot. NativeWindow uses exact-HWND WGC,
GPU crop/resize and bounded CPU JPEG encoding; it has no picker or BrowserViewport fallback.
LCWB launches request --silent-debugger-extension-api as Chrome-dependent best-effort
notice suppression only. Permission remains broad; existing profiles may retain old
flags and Chrome may ignore it. Capture does not require suppression. Visual infobar
absence was not established as a guarantee. No DOM/Runtime/Network inspection or input automation.

Downloads belong to the Chrome profile, not an application session. Consumers own
filesystem checks after Complete. Monitoring is for human viewing only; there is no
DOM automation, OCR or webpage-completion detection. Initial offscreen capture may
time out; explicit Start retries the affected generation. See the repository's
[integration](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/blob/main/docs/integration.md)
and [limitations](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/blob/main/docs/limitations.md).
MIT licensed.

## Maintainer validation and publication boundary

Run `./Scripts/Test-NuGet.ps1` with PowerShell 7 on Windows. It uses the stable .NET
10.0.4xx servicing line with 10.0.401 as the global.json floor and latestPatch roll-forward,
restores and builds Release, runs the complete Core/public API/extension checks, packs
both Core target assets, inspects both package archives and runs public API checks
again from an isolated local-feed PackageReference consumer. It never publishes.
Generated packages and consumer work stay under ignored `artifacts/nuget/0.3.1`; the
single invocation log stays under `Scripts/Outputs`.

Trusted-publishing policy: `LazyChromeWindowBridge-publish`; NuGet owner `Yasuhiko-m`;
scope Push new packages and package versions; glob `LazyChromeWindowBridge.*`.
The exact publisher tuple is `Yasuhiko-m / LazyChromeWindowBridge / publish-nuget.yml / release`.
The workflow is manual-dispatch only on this repository's main branch, checks out the
dispatch SHA, and has only `contents: read` and `id-token: write` permissions. OIDC
login happens after local validation; no persistent API key or GitHub secret is used.
A duplicate version fails. The exact Core push lets the NuGet CLI send its adjacent
matching portable-PDB `.snupkg`; there is no wildcard, duplicate suppression, or
separate symbol-push command.

This release-preparation task does not create a GitHub tag or Release, dispatch the
workflow, authenticate to NuGet, or publish a package. Those Controller operations occur
only after Chat acceptance and checkpoint verification.
