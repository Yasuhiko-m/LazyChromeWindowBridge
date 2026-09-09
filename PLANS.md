# LazyChromeExtension PLANS

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

### M001 — Session-bound browser window
Goal: CallerHarness launches/initiates a Chrome WebApp session and maintains a
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
- CallerHarness preview and the human-view-only boundary.
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
Status: **In Progress**. R006 productize-refactor is the final V0 candidate Revision.
ProjectID and registered Source stay LazyChromeExtension. GitHub creation/push waits
until Chat accepts R006.

## Planning rules
- This file contains Version/Milestone roadmap only; no moving Current VMR.
- PLAN.md and Revisions.md retain the established R005 authority during candidate work.
- Chat defines revisions during milestone execution; future numbers are not predefined.
- Same-purpose retries use a flat issuance suffix, never nested suffixes.
- At milestone boundaries review evidence, remaining issues, and discoveries.
- Future versions remain deferred until V0 evidence supports a concrete next goal.
