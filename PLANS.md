# LazyChromeExtension PLANS

## Project goal
Establish a Windows caller and Chrome extension integration that keeps a launched
Web application session associated with its window across ordinary navigation.

## Version roadmap

### V0 — Browser-session integration foundation
Goal:
Prove and establish the browser-session integration foundation, initially with
ChatGPT in Chrome and CallerHarness as the reference/test launching application.

Exit Criteria:
- The caller/extension foundation can be built, launched, and loaded locally.
- Session/window ownership survives ordinary navigation for the window's lifetime.
- Window geometry can be stored/restored according to the launch URL/profile.
- Human-only visual-monitoring feasibility is evaluated within the adopted boundaries;
  limitations and any proposed mechanism are reviewed before adoption.

Milestones:

| Milestone | Goal | Exit Criteria | Status |
| --- | --- | --- | --- |
| M001 — Session-bound browser-window foundation | Establish the scaffold, then select transport and bind caller sessions to launched Chrome windows. | Caller builds/runs, extension loads, and session/window ownership survives navigation in an agreed validation flow. | In Progress |
| M002 — Window geometry | Store and restore position/size according to launch URL/profile. | Agreed geometry behavior is demonstrated without redefining session ownership. | Planned |
| M003 — Human-only visual-monitoring investigation | Evaluate monitoring while a window is away from the normal visible desktop area. | Human-visible feasibility and limitations are documented; no DOM/OCR/semantic extraction is used; mechanism adoption is a separate decision. | Planned |

This roadmap does not advance runtime planning state. PLAN.md retains its existing
bootstrap milestone goal and baseline until Chat acceptance updates planning.

## Planning rules
- PLANS.md contains Version/Milestone roadmap only.
- A Milestone is an independently verifiable feature group / arrival goal.
- Keep Milestones small enough to have a clear completion decision.
- Do not mass-predefine Revision numbers here.
- Do not store Current Revision or Current VMR here.
- Chat defines Revisions while executing a Milestone.
- Same-purpose retry uses one flat issuance counter: `R001`, `R001_1`, `R001_2`; never nested `R001_1_1`. Historical nested values remain readable compatibility only.
- At a Milestone boundary, review achieved behavior, remaining issues, discoveries, and planning changes before finalizing the next Milestone.

## Future versions
Deferred until V0 evidence supports a concrete next goal.
