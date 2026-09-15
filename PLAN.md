# LazyChromeExtension PLAN

- Current Version: `V1`
- Current Milestone: `M008`
- Milestone Goal: v0.3.0 Distribution and Publication Preparation
- Current Revision: `R014`
- Current Baseline: `V1-M008-R014`
- Milestone Status: **Complete**
- Version Status: **In Progress**

## Handoff

Chat accepted `V1-M008-R014` (v0.3.0-distribution-publication). M008 is Complete;
V1 remains In Progress and V0 remains Complete. Revisions.md establishes
V1-M008-R014 as Current VMR.

Runtime behavior remains the accepted R013 0.3.0 contract. R014 establishes the
distribution/publication preparation: Core-only NuGet 0.3.0, matching Extension delivery,
GitHub Release fallback when Chrome Web Store review lags, deterministic Extension ZIP,
self-contained Windows x64 bundle and strict NuGet package/README verification.

Windows validation with SDK 10.0.400 passed Core 179 / external PublicApi 35 / Extension
40 checks, isolated local-feed consumer, deterministic Extension packaging and Windows
bundle inventory/extraction. Pre-checkpoint artifact hashes are recorded in Revisions.md
and must not be published as final bytes because NuGet provenance must identify the
accepted R014 checkpoint.

The next boundary is the independent R014 Git checkpoint/push, followed by the user's
already-authorized GitHub v0.3.0 Release and NuGet 0.3.0 publication. Final artifacts
must be rebuilt from that exact checkpoint before publication.

GitHub/NuGet 0.1.0 and 0.2.0 remain immutable historical releases. CWS 0.2.0 was
submitted for review with automatic publication requested after approval; its current
approval/publication state is not asserted here and R014 performs no CWS operation.

The known third-display initial offscreen BrowserViewport timeout remains unresolved.
The --silent-debugger-extension-api launch flag remains best-effort suppression only.

This legacy file mirrors the established baseline.