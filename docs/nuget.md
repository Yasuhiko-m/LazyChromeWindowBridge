# LazyChromeWindowBridge.Core

Windows-only .NET 10 Core library for session-bound Chrome windows, native geometry,
PARK/RESTORE, human-only thumbnails and profile-global download lifecycle events.
**0.2.0 is a prepared release candidate, not a published NuGet version.** It packages
the accepted R010/R011 monitoring capabilities. Published NuGet 0.1.0 from R009 remains
an older baseline and does not contain those features. This is a feature release:
the additive public API and expanded monitoring contract warrant a minor version,
not a patch. Existing ownership, geometry and downloads contracts are retained;
compatibility is bounded by the tested consumer/regression coverage.

## Consumer setup

For this candidate, add `LazyChromeWindowBridge.Core` version `0.2.0` from the prepared local feed to a
`net10.0-windows` application. The package carries the ASP.NET Core and Windows
Desktop framework references; those shared runtimes are required for a framework-
dependent consumer. The Core assembly does not expose WinForms/WPF UI types.

The Chrome extension is distributed separately; install the matching unpacked
extension in the intended Chrome profile. The NuGet package contains only Core,
its metadata and this README, not Chrome, SampleCaller or the extension.
Consumer UI code must wait for a bound native window before PARK/RESTORE.

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
bridge.StopMonitoring(); // Batch stop; clears all JPEGs, retains restart sockets.
```

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
ordinary options updates preserve the monitor connection and same-tab debugger.
Production debugger commands are exactly Page.getLayoutMetrics and Page.captureScreenshot.
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

## Maintainer validation and publishing

Run `./Scripts/Test-NuGet.ps1` with PowerShell 7 on Windows. It uses SDK 10.0.400,
restores and builds Release, runs the existing 154 Core / 29 public API / 39 extension
checks, packs Core, inspects both package archives and runs the public API checks
again from an isolated local-feed PackageReference consumer. It never publishes.
Generated packages and consumer work stay under ignored `artifacts/nuget/0.2.0`; the
single invocation log stays under `Scripts/Outputs`.

Trusted-publishing policy: `LazyChromeWindowBridge-publish`; NuGet owner `Yasuhiko-m`;
scope Push new packages and package versions; glob `LazyChromeWindowBridge.*`.
The exact publisher tuple is `Yasuhiko-m / LazyChromeWindowBridge / publish-nuget.yml / release`.
The workflow can run only by manual dispatch on this repository's main branch.
It checks out the dispatch SHA, not a moving branch or a supplied arbitrary ref.
The release job has only `contents: read` and `id-token: write` permissions.

The OIDC login occurs after validation. Its short-lived output is passed only to the
push step's environment; no persistent API key, NuGet credential configuration or
GitHub secret is required. A duplicate version fails. Do not retry blindly after a
partial package/symbol push; inspect nuget.org first. The workflow does not modify
GitHub tags, Releases or repository visibility.

Pack produces one Core `.nupkg` and one matching `.snupkg` with portable PDBs and
SDK Source Link metadata. The single exact `.nupkg` push uses the NuGet V3 endpoint;
the CLI discovers and sends the adjacent matching `.snupkg`. There is no separate
symbol-push command. See the official
[symbol package documentation](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg)
and [Trusted Publishing documentation](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

R009 publication is historical; its package is immutable. R010/R011 are accepted
Source in the combined R011 checkpoint. Git checkpoint/push does not authorize
workflow dispatch or NuGet publication.
Chat accepted R012 preparation; M006 is Complete. This does not authorize publication.
Accepted pre-checkpoint package hashes are validation evidence, not final publication
hashes. Their repository/Source Link metadata identifies the older R011 Git HEAD.
After the independent R012 checkpoint is created and pushed, rebuild and verify from
that exact checkpoint before separately authorized publication. The final package
must identify the publication checkpoint; the prepared version is not yet published.
