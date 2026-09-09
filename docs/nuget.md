# LazyChromeWindowBridge.Core

Windows-only .NET 10 Core library for session-bound Chrome windows, native geometry,
PARK/RESTORE, human-only thumbnails and profile-global download lifecycle events.
The first prepared NuGet version is **0.1.0**. Candidate preparation does not imply
that this version has been published to nuget.org.

## Consumer setup

After publication, add `LazyChromeWindowBridge.Core` version `0.1.0` to a
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

Downloads belong to the Chrome profile, not an application session. Consumers own
filesystem checks after Complete. Monitoring is for human viewing only; there is no
DOM automation, OCR or webpage-completion detection. Initial offscreen capture may
time out; explicit Start retries the affected generation. See the repository's
[integration](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/blob/main/docs/integration.md)
and [limitations](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/blob/main/docs/limitations.md).
MIT licensed.

## Maintainer validation and publishing

Run `./Scripts/Test-NuGet.ps1` with PowerShell 7 on Windows. It uses SDK 10.0.400,
restores and builds Release, runs the existing 121 Core / 19 public API / 33 extension
checks, packs Core, inspects both package archives and runs the public API checks
again from an isolated local-feed PackageReference consumer. It never publishes.
Generated packages and consumer work stay under ignored `artifacts/nuget`; the
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

R009 remains a candidate until Chat accepts it. Do not commit, push, dispatch this
workflow or publish to nuget.org during candidate work. After acceptance and push,
the separately authorized closeout manually dispatches and verifies NuGet indexing.
