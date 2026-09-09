# LazyChromeWindowBridge PLANS

## Project goal
Complete a Windows caller / Chrome extension reference implementation that keeps
an application session associated with its own browser window across navigation.

## V0 — Complete the reference implementation
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

## Planning rules
- This file contains Version/Milestone roadmap only; no moving Current VMR.
- Revisions.md establishes accepted V1-M003-R009; PLAN.md mirrors it. V0 is complete.
- Chat defines revisions during milestone execution; future numbers are not predefined.
- Same-purpose retries use a flat issuance suffix, never nested suffixes.
- At milestone boundaries review evidence, remaining issues, and discoveries.
- Future versions remain deferred until V0 evidence supports a concrete next goal.
