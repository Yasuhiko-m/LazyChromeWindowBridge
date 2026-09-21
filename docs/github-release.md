# GitHub v0.3.2 publication record and reproducibility

GitHub `v0.3.2` was published from clean checkpoint
`d490bdbbfd6c98755388a64e288cc8ac8989a98a`:
<https://github.com/Yasuhiko-m/LazyChromeWindowBridge/releases/tag/v0.3.2>.
GitHub/NuGet 0.1.0–0.3.1 remain historical releases. CWS 0.2.0 is general-public but
does not match Core/Extension 0.3.x; Chrome Web Store 0.3.2 is manually submitted and
pending review, not yet public.

## Published release assets

| Asset | Bytes | SHA256 |
| --- | ---: | --- |
| `LazyChromeWindowBridge.Extension-0.3.2-cws.zip` | 38,039 | `EF7BD7DC6140C3DAFD84715F83F7A312C6292BAA8EC0C919D5E44995B3B307EE` |
| `LazyChromeWindowBridge-v0.3.2-win-x64.zip` | 95,134,522 | `F360989805790D3537E2D1CF9E71E855E399DDA719E40483178724A259D3CEDC` |

## NuGet publication evidence

The immutable-tag manual `Publish NuGet` run
[`35521128215`](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/actions/runs/35521128215)
completed successfully from `v0.3.2` at the release checkpoint, including exact checkout
verification, validation, NuGet OIDC login, and verified Core package push. The
checkpoint-built package evidence was:

| Package | Bytes | SHA256 |
| --- | ---: | --- |
| `LazyChromeWindowBridge.Core.0.3.2.nupkg` | 142,872 | `A0004D6023A47F1661881B0059B7E070E222226FF8B7C197B9B810A7BEA35427` |
| `LazyChromeWindowBridge.Core.0.3.2.snupkg` | 62,155 | `27525E8D0DE85A481441CCAF1EEF87D5CA50EE7EEE4F527C5145AE8A7C65CCF8` |

This records successful immutable-tag OIDC publication; NuGet Gallery indexing/visibility
was not independently measured for 0.3.2. An initial Controller dispatch check failed
before any workflow ran because it compared an annotated tag object directly with the
checkpoint commit. The Controller-only dereference fix and retry succeeded; no LCWB
Source runtime defect was involved.

## Reproducibility procedure

For a future release, rebuild from the accepted checkpoint with PowerShell 7 and the
stable .NET 10.0.4xx servicing line, then run:

```powershell
./Scripts/Prepare-Release.ps1
```

Use [the v0.3.2 release notes](releases/v0.3.2.md) as release text. The Extension ZIP is
valid for extract plus **Load unpacked**; NuGet remains Core-only. `Prepare-Release.ps1`
and `Test-NuGet.ps1` validate/package locally and never publish. The immutable-tag OIDC
workflow is the NuGet publication boundary.

## User distribution wording

Users install Core `0.3.2` and the matching extension. The Store is convenient only when
it has the matching version. CWS currently carries general-public 0.2.0, not 0.3.2, so
users download the matching Extension ZIP from GitHub `v0.3.2`, extract it, open
`chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select
the extracted directory. CWS review may lag or skip versions; optional CWS V2 submission
remains a separate configured operation.

## Historical records

Historical procedures and distribution evidence remain under `docs/releases/`, including
the completed v0.3.1 publication record.
