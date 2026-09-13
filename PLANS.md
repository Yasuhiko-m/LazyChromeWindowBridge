# LazyChromeWindowBridge PLANS

## Project goal
Complete a Windows caller / Chrome extension reference implementation that keeps
an application session associated with its own browser window across navigation.

## V0 — Complete the reference implementation
Historical accepted scope and evidence below; the continuous-monitor goal is V1 M004.
Goal: complete the reference flow, initially for a Chrome WebApp such as ChatGPT,
while keeping session identity independent of page content and navigation.

Exit Criteria:
- Real launch and deterministic session/window ownership.
- Stable ownership across navigation and simultaneous sessions.
- Geometry persistence and exact PARK / RESTORE behavior.
- Human-only visual monitoring and integrated acceptance of the complete flow.

Status: **Complete**. Chat accepted V0-M005-R006 as the final V0 Source Revision.
M001 through M005 are complete; later product work requires a new task.

### M001 — Session-bound browser window
Goal: the Windows sample launches/initiates a Chrome WebApp session and maintains a
stable appSessionId ↔ Chrome Window relationship across ordinary page navigation.

Exit Criteria:
- Real launch flow works.
- Session/window binding is deterministic.
- Navigation does not break or redefine binding.
- At least two simultaneous sessions do not cross-bind.
- Window close terminates its session binding.
- MV3 service-worker lifecycle does not lose an active binding.

Status: **Complete**. Chat accepted R002 and its session/window integration evidence.

### M002 — Geometry / Park / Restore
Goal: persist/restore geometry by launch URL/profile and implement the special
offscreen PARK → RESTORE lifecycle.

Scope when this milestone begins:
- x/y/width/height; multi-monitor, negative coordinates, and DPI.
- Launch URL profile.
- Normal geometry must not be polluted by PARK geometry.
- Native SetWindowPos may be used if required.
- Complete offscreen PARK and exact RESTORE.

Exit Criteria: the adopted geometry profile and full PARK / RESTORE behavior pass
validation across supported monitor/DPI configurations without altering ownership.

Status: **Complete**. Chat accepted R003 and its geometry/PARK/RESTORE evidence.

### M003 — Human monitor + integrated V0 acceptance
Goal: provide human-only visual monitoring of the PARKED WebApp and complete the
end-to-end V0 reference flow.

Scope when this milestone begins:
- Capture/stream implementation.
- Practical resolution, fps, and load measurement.
- the Windows sample preview and the human-view-only boundary.
- Integrated flow: launch → bind → restore geometry → PARK → monitor → RESTORE.

Exit Criteria: the complete integrated flow passes acceptance with usable monitoring
and measured limits; no DOM/OCR/semantic/output extraction or page-content automation.

Status: **Complete**. Chat accepted R004 and its real offscreen monitoring evidence.

### M004 — Final runtime tuning / multi-window monitor / manual geometry control
Goal: align runtime behavior with the intended consumer workload before productization.
Scope: simultaneous PARKED-session thumbnails, zero Visible capture, small images
and empirical native PARK-size evaluation, and Windows-side manual Get/Set bounds.
Exit criteria: one ACTIVE plus four independent PARKED sessions pass freshness,
ownership, geometry, cleanup and measured aggregate-load acceptance.
Status: **Complete**. Chat accepted R005: 96 caller checks, 17 extension checks,
prior regressions, five-session real-browser workload and WinForms GUI PASS.

### M005 — Productization / complete refactor / GitHub readiness
Goal: adopt final product name LazyChromeWindowBridge; complete class/file/directory/
namespace/project rename; reusable Core / Extension / Sample separation; public
documentation/security/setup; private GitHub baseline and later public release.
LazyAIDeck is a consumer, not the owner/container of this project.
Status: **Complete**. Chat accepted R006 productize-refactor as
the final V0 Source Revision: renamed clean build, 96 Core + 17 public API checks,
17 extension tests, complete real-browser regressions and five-session sample GUI PASS.
Initial offscreen capture timeout observations remain documented limitations.
ProjectID and registered Source stay LazyChromeExtension. Private GitHub creation,
main push and the v0.1.0 baseline tag are explicitly authorized after the accepted
checkpoint. Public visibility and licensing remain deferred.

## V1 — Browser application state observation
Status: **In Progress**.

### M001 — Download Lifecycle Notification
Goal: read-only Chrome-profile-global Created / Complete / Interrupted observation
through the existing authenticated Bridge, without webpage inspection or guessed
session attribution. Bounded delivery survives ordinary MV3 restart and temporary
loopback failure within the existing browser session. Consumers own filesystem checks.
Exit criteria: deterministic and real download/worker/five-session/consumer acceptance
and all existing regressions pass, with documented capacity and privacy boundaries.
Status: **Complete**. Chat accepted R007: 121 Core / 19 public API / 33 extension
checks and complete real browser/native/monitor/download acceptance PASS.

### M002 — Public Release Preparation
Goal: prepare MIT licensing, public developer documentation, a neutral hero screenshot,
release notes and a deterministic extension ZIP without new runtime features.
Status: **Complete**. Chat accepted R008: MIT license, public README/docs, reviewed
neutral hero image and deterministic six-file extension ZIP. Final clean validation
passed 121 Core / 19 public API / 33 extension checks and all real browser regressions
on a fresh-profile rerun; the initial run reproduced the documented capture timeout.
The user explicitly authorizes normal push, PUBLIC visibility and Release v0.1.0.
Publication is a subsequent operation verified against the accepted checkpoint.

### M003 — Official Package Distribution
Goal: prepare the Core 0.1.0 NuGet package and Chrome Web Store extension 0.1.0
distribution without changing runtime functionality.
Status: **Complete**. Chat accepted R009: package metadata/readme/icon/symbols,
manual-only SHA-pinned OIDC Trusted Publishing, original extension icon family,
real 1280x800 store screenshot, 440x280 promo, privacy policy, dashboard/reviewer
copy and deterministic ten-file CWS ZIP. Clean standard full validation passed
121 Core / 19 public API / 33 extension checks, local NuGet consumer and complete
real browser/native/monitor/download regressions. The third-display initial capture
timeout reproduced twice and remains unresolved; standard primary-display tests
and the third-display real GUI capture passed. No assertions/timeouts were weakened.
NuGet publication follows the accepted checkpoint, normal push and duplicate-version
preflight. CWS upload and submission remain deferred to an interactive user task.
The existing GitHub v0.1.0 tag/Release and runtime functionality are unchanged.

### M004 — Continuous Monitor Preview
Goal: use the same human-view JPEG preview for live owned Visible and Parked windows.
Placement changes and in-place CaptureOptions updates preserve monitor generation,
latest frame, WebSocket and same-tab debugger attachment. Allow requests of 1–30 fps
(default 2, no throughput guarantee), default 240×135, aspect-preserving output bounds,
no upscale and fixed quality70 without changing native size, viewport or zoom.
Status: **Complete**. Accepted by Chat at V1-M004-R010.
The accepted implementation is included in the combined R010/R011 Git checkpoint.
Exit criteria: deterministic Core/public API/extension checks and real Windows evidence
for Visible capture, repeated placement continuity, live options, single-session 30fps
request, five mixed-placement streams, cleanup, all existing regressions and sample GUI.
Keep the known third-display initial capture timeout explicit. Implementation acceptance
is separate from Git checkpoint/push and package or Chrome Web Store publication.

### M005 — Caller-controlled Session Monitoring
Goal: caller-owned per-session pause/resume alongside global batch monitor controls.
Paused sessions retain a frozen last JPEG and waiting socket; placement never auto-toggles
monitoring. LCWB launches include --silent-debugger-extension-api for best-effort suppression
without changing extension permissions or the two-command production capture boundary.
Status: **Complete**. Accepted by Chat at V1-M005-R011.
Exit criteria: deterministic and real five-session pause/resume/peer/frozen-frame evidence,
R010 continuity, global batch restart/shutdown, launch-flag command-line verification,
SampleCaller GUI, updated current/future distribution copy and all existing regressions.
Git checkpoint/push is separate from package publication, tag changes and CWS dashboard actions.
The known third-display initial capture timeout remains unresolved. Silent flag
suppression is best-effort; visual infobar absence was not established in acceptance.
Both accepted milestones share the R011 checkpoint; package/release publication is
separate and V1 remains In Progress.

### M006 — v0.2.0 Distribution Preparation
Goal: prepare one coherent 0.2.0 GitHub / NuGet / Chrome Web Store distribution
candidate from the accepted R011 baseline, with exact packages, a Windows x64 bundle,
current reviewed Store screenshot and complete distribution validation.
Status: **Complete**. Accepted by Chat at V1-M006-R012; V1 remains In Progress.
The coherent 0.2.0 candidate and full distribution validation are accepted.
Preparation acceptance is separate from Git checkpoint and publication. Rebuild
publication artifacts from the exact accepted checkpoint; pre-checkpoint hashes are
validation evidence only. No tag/Release, NuGet publication or live CWS operation
belongs to this authority closeout.

## Planning rules
- This file contains Version/Milestone roadmap only; no moving Current VMR.
- Revisions.md establishes accepted V1-M006-R012; PLAN.md mirrors it. V0 is complete.
- Chat defines revisions during milestone execution; future numbers are not predefined.
- Same-purpose retries use a flat issuance suffix, never nested suffixes.
- At milestone boundaries review evidence, remaining issues, and discoveries.
- Future versions remain deferred until V0 evidence supports a concrete next goal.
