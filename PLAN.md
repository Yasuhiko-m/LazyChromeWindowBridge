# LazyChromeExtension PLAN

- Current Version: `V1`
- Current Milestone: `M011`
- Milestone Goal: v0.3.1 Release Preparation
- Current Revision: `R022`
- Current Baseline: `V1-M011-R022`
- Milestone Status: **Complete**
- Version Status: **In Progress**

## Handoff

Chat accepted `V1-M011-R022` (v0.3.1-publication-closeout). M009, M010 and M011 are
Complete; V1 remains In Progress and V0 remains Complete. Revisions.md establishes
V1-M011-R022 as Current VMR.

Source version 0.3.1 is established: caller-controlled Chrome launch/background policy,
natural BrowserViewport capture with region/resize/source-size APIs, exact-HWND
NativeWindow GPU processing, and observational preview-liveness hardening are accepted.
R018 establishes release-ready distribution documentation and package consistency. R019
is an infrastructure-only validation-map repair. R021 adopts the stable 10.0.4xx
servicing-line toolchain policy only; 0.3.1 product behavior remains accepted from R018/R019.
R022 closes out the accepted documentation update for the completed GitHub v0.3.1
Release and successful NuGet OIDC workflow/push from checkpoint
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd`. NuGet Gallery indexing was not
independently measured. CWS 0.3.1 remains unpublished and the historical submitted
CWS 0.2.0 review remains untouched. The known third-display initial offscreen
BrowserViewport timeout and Chrome-dependent silent-debugger suppression remain explicit.

This legacy file mirrors the established baseline.
