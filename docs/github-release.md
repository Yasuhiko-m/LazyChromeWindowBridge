# GitHub v0.3.0 release procedure

This is the Controller procedure after Chat accepts R014 and creates the independent
checkpoint. It is not an authorization to run publication operations during Source
preparation. GitHub/NuGet 0.1.0 and 0.2.0 are historical publications; the submitted
CWS 0.2.0 review remains untouched.

## Required assets

Rebuild assets from the accepted publication checkpoint with PowerShell 7 on Windows:

```powershell
./Scripts/Test-NuGet.ps1
./Scripts/Package-Extension.ps1
./Scripts/Package-Windows.ps1
```

Attach exactly these generated assets to GitHub Release `v0.3.0`:

| Asset | Path |
| --- | --- |
| Matching unpacked extension ZIP | `artifacts/cws/LazyChromeWindowBridge.Extension-0.3.0-cws.zip` |
| Windows x64 self-contained bundle | `artifacts/release/0.3.0/LazyChromeWindowBridge-v0.3.0-win-x64.zip` |

Use [the v0.3.0 release notes](releases/v0.3.0.md) as the release text. The extension
asset is already valid for extract plus **Load unpacked**; do not create a duplicate
asset alias, CRX, installer, updater, or another extension payload. NuGet remains
Core-only, so do not add this ZIP or its files to the `.nupkg`.

## Preflight and ordered publication

1. Verify the accepted checkpoint is on `main`, `origin/main` resolves to it, and no
   conflicting local or remote `v0.3.0` tag or GitHub Release exists.
2. Run the commands above, inspect their exact artifacts, and verify the Core `.nupkg`
   and adjacent `.snupkg` describe the accepted checkpoint through repository metadata
   and Source Link.
3. Create and push the immutable `v0.3.0` tag for that checkpoint, then create the
   GitHub Release with both listed assets and the release notes.
4. Dispatch only the manual `publish-nuget.yml` workflow from `main`. Its `release`
   environment uses OIDC Trusted Publishing and pushes exactly
   `LazyChromeWindowBridge.Core.0.3.0.nupkg`; duplicate versions fail.
5. Confirm the GitHub asset names and NuGet package version. Do not operate the Chrome
   Web Store item in this procedure.

## User distribution wording

Users install Core `0.3.0` from NuGet and use the matching Chrome extension. The Chrome
Web Store is the normal convenient extension path when its matching version is available.
Store review can lag. If the Store does not offer matching 0.3.0, users download
`LazyChromeWindowBridge.Extension-0.3.0-cws.zip` from GitHub Release `v0.3.0`, extract
it, open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**,
and select the extracted directory. Do not imply that the older CWS 0.2.0 build matches
Core 0.3.0, or that CWS 0.3.0 is approved or published.

## Historical records

The 0.1.0 release procedure and distribution evidence remain historical at
[v0.1.0](releases/v0.1.0.md) and
[v0.1.0 distribution](releases/v0.1.0-distribution.md). The immutable 0.2.0 history
is retained at [v0.2.0](releases/v0.2.0.md) and
[v0.2.0 distribution](releases/v0.2.0-distribution.md). Current preparation evidence
is [v0.3.0 distribution](releases/v0.3.0-distribution.md).
