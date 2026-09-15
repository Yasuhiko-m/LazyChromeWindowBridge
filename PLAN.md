# LazyChromeExtension PLAN

- Current Version: `V1`
- Current Milestone: `M007`
- Milestone Goal: Native Window Capture and Taskbar Policy
- Current Revision: `R013`
- Current Baseline: `V1-M007-R013`
- Milestone Status: **Complete**
- Version Status: **In Progress**

## Handoff

Chat accepted `V1-M007-R013` (native-window-capture-taskbar). M007 is Complete;
V1 remains In Progress and V0 remains Complete. Revisions.md establishes
V1-M007-R013 as Current VMR.

The accepted Source version is 0.3.0 and remains unpublished. BrowserViewport stays
the source-compatible default; NativeWindow adds exact-HWND WGC/D3D11 capture without
picker/CDP screenshot fallback, and SetShowInTaskbar adds independent exact-HWND taskbar
policy with normal-disposal restoration. PARK/RESTORE never changes monitoring policy.

Implementation acceptance is separate from Git checkpoint/push and publication. At this
authority closeout HEAD is still the accepted V1-M006-R012 checkpoint; the next boundary
is an independent R013 checkpoint/push after full-worktree and staged hygiene gates.

GitHub and NuGet 0.2.0 are already published. CWS 0.2.0 was submitted for review on
2026-09-13 with automatic publication requested after approval; approval/publication is
not asserted here. No 0.3.0 tag, Release, NuGet publication or CWS operation is authorized
by this authority closeout.

Explorer taskbar-button pixels were not separately inspected; exact-HWND style/native-call
isolation and restoration evidence is accepted with that boundary. The third-display initial
offscreen BrowserViewport timeout remains unresolved. The --silent-debugger-extension-api
launch flag remains best-effort suppression only.

This legacy file mirrors the established baseline.
