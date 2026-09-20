# GitHub v0.3.1 publication record and reproducibility

GitHub `v0.3.1` was published from checkpoint
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd` on 2026-09-16T02:43:20Z. It is a
non-draft, non-prerelease release:
<https://github.com/Yasuhiko-m/LazyChromeWindowBridge/releases/tag/v0.3.1>.
GitHub/NuGet 0.1.0–0.3.0 remain historical releases. CWS 0.2.0 is general-public but
does not match Core/Extension 0.3.x; Chrome Web Store publication is outside this record.

## Published release assets

| Asset | Bytes | SHA256 |
| --- | ---: | --- |
| `LazyChromeWindowBridge.Extension-0.3.1-cws.zip` | 34,137 | `BEE26DEF8F958934DA7305E4D76B08A3C5FEBB193B8AC284999745B91EB62999` |
| `LazyChromeWindowBridge-v0.3.1-win-x64.zip` | 95,128,807 | `C065D57426696C8A338F7A4FC62D099994A78F8B8E04995ED50FCAD116A131AD` |

## NuGet publication evidence

The manual `Publish NuGet` GitHub Actions run `35049074171` completed
successfully, including exact checkout verification, SDK and Node setup,
`Test-NuGet` validation, NuGet OIDC login, and the verified Core package push.

| Package | Bytes | SHA256 |
| --- | ---: | --- |
| `LazyChromeWindowBridge.Core.0.3.1.nupkg` | 134,268 | `7A09659E182620A1F77965ADB04D0ED18E2A7F3821B545CD93CFBC8066E2D5A2` |
| `LazyChromeWindowBridge.Core.0.3.1.snupkg` | 60,507 | `21FAC31D39AD8DA54845843253D5EC4EADA1D870EFBFDBF39DD4FA53BFF4B6FE` |

The package repository commit is
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd`. Core 0.3.1 is independently visible/indexed
on NuGet Gallery; this record preserves the successful workflow/push evidence.

## Reproducibility procedure

For a future release, rebuild assets from the accepted checkpoint with PowerShell
7 and the stable .NET 10.0.4xx servicing line (10.0.401 floor, latestPatch
roll-forward):

```powershell
./Scripts/Test-NuGet.ps1
./Scripts/Package-Extension.ps1
./Scripts/Package-Windows.ps1
```

Use [the v0.3.1 release notes](releases/v0.3.1.md) as release text. The extension ZIP is
valid for extract plus **Load unpacked**; do not add a duplicate alias, CRX, installer,
updater, or extension payload to NuGet. NuGet remains Core-only.
`Scripts/Test-NuGet.ps1` validates and packs Core; it does not publish. The
manual `Publish NuGet` workflow is the OIDC publication boundary and pushes the
exact verified Core package.

## User distribution wording

Users install Core `0.3.1` from NuGet and use the matching extension. The Store is the
convenient path only when it has the matching version. Store review can lag; if it does,
users download `LazyChromeWindowBridge.Extension-0.3.1-cws.zip` from GitHub Release
`v0.3.1`, extract it, open `chrome://extensions`, enable **Developer mode**, choose
**Load unpacked**, and select the extracted directory. Do not imply CWS 0.2.0 or any
other Store version automatically matches Core 0.3.1, or that CWS 0.3.1 is approved.

## Historical records

Historical release procedures and distribution evidence remain under `docs/releases/`.
Chrome Web Store 0.3.1 was not part of this publication and remains unpublished.
