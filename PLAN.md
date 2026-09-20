# LazyChromeExtension PLAN

- Current Version: `V1`
- Current Milestone: `M013`
- Milestone Goal: Release automation / version-coherent distribution
- Current Revision: `R029`
- Current Baseline: `V1-M013-R029`
- Milestone Status: **Complete**
- Version Status: **In Progress**

## Handoff

Chat accepted R027_1 as the M013 implementation endpoint and R028 as its authority
closeout. R029 normalizes the established baseline to `V1-M013-R029` for deterministic
checkpointing. M009–M013 are Complete; V1 remains In Progress and V0 remains Complete.
Revisions.md establishes V1-M013-R029 as Current VMR.

Source version 0.3.2 is established. It retains the accepted 0.3.1 caller-controlled Chrome launch/background policy,
natural BrowserViewport capture with region/resize/source-size APIs, exact-HWND
NativeWindow GPU processing, and observational preview-liveness hardening are accepted.
R018 establishes release-ready distribution documentation and package consistency. R019
is an infrastructure-only validation-map repair. R021 adopts the stable 10.0.4xx
servicing-line toolchain policy only; 0.3.1 product behavior remains accepted from R018/R019.
R022 closes out the accepted documentation update for the completed GitHub v0.3.1
Release and successful NuGet OIDC workflow/push from checkpoint
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd`; Core 0.3.1 is now independently confirmed
visible/indexed on NuGet Gallery. CWS 0.2.0 is general-public but does not match Core/
Extension 0.3.x; CWS 0.3.1 and Source 0.3.2 remain unpublished. M012 adds one bounded
structured allowlisted key/chord request for the exact owned active tab only, with no
macro, arbitrary automation or observed page effect. The known third-display initial offscreen
BrowserViewport timeout and Chrome-dependent silent-debugger suppression remain explicit.
M013 adds accepted local version-driven preparation and bounded Controller publication
allowlists: checkpoint/push, rebuild from the accepted tag, GitHub/NuGet publication, and
optional configured existing-item CWS V2 submission remain separate operational steps.

This legacy file mirrors the established baseline.
