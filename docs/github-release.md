# GitHub v0.3.1 release procedure — publication pending

This is the Controller procedure after Chat accepts the 0.3.1 working tree and creates
the independent checkpoint. It is not authorization to publish during Source preparation.
GitHub/NuGet 0.1.0–0.3.0 are historical releases; the submitted CWS 0.2.0 review remains
untouched. Chrome Web Store publication is outside this procedure.

## Required assets

Rebuild assets from the accepted checkpoint with PowerShell 7 and the stable .NET 10.0.4xx
servicing line (10.0.401 floor, latestPatch roll-forward) on Windows:

```powershell
./Scripts/Test-NuGet.ps1
./Scripts/Package-Extension.ps1
./Scripts/Package-Windows.ps1
```

Attach exactly these generated assets to GitHub Release `v0.3.1`:

| Asset | Path |
| --- | --- |
| Matching unpacked extension ZIP | `artifacts/cws/LazyChromeWindowBridge.Extension-0.3.1-cws.zip` |
| Windows x64 self-contained bundle | `artifacts/release/0.3.1/LazyChromeWindowBridge-v0.3.1-win-x64.zip` |

Use [the v0.3.1 release notes](releases/v0.3.1.md) as release text. The extension ZIP is
valid for extract plus **Load unpacked**; do not add a duplicate alias, CRX, installer,
updater, or extension payload to NuGet. NuGet remains Core-only.

## Ordered publication

1. Verify the accepted checkpoint is on `main`, `origin/main` resolves to it, and no
   conflicting `v0.3.1` tag or GitHub Release exists.
2. Run the commands above and inspect the exact artifacts. Verify the Core `.nupkg` and
   matching `.snupkg` repository commit and Source Link match the checkpoint.
3. Create/push immutable tag `v0.3.1`, then create the GitHub Release with both assets.
4. Dispatch only manual `publish-nuget.yml` from `main`. Its `release` environment uses
   OIDC Trusted Publishing and pushes exactly `LazyChromeWindowBridge.Core.0.3.1.nupkg`;
   duplicate versions fail.
5. Confirm GitHub asset names and NuGet package version. Do not operate the CWS item.

## User distribution wording

Users install Core `0.3.1` from NuGet and use the matching extension. The Store is the
convenient path only when it has the matching version. Store review can lag; if it does,
users download `LazyChromeWindowBridge.Extension-0.3.1-cws.zip` from GitHub Release
`v0.3.1`, extract it, open `chrome://extensions`, enable **Developer mode**, choose
**Load unpacked**, and select the extracted directory. Do not imply CWS 0.2.0 or any
other Store version automatically matches Core 0.3.1, or that CWS 0.3.1 is approved.

## Historical records

Historical release procedures and distribution evidence remain under `docs/releases/`.
Their facts are not rewritten by this v0.3.1 publication preparation.
