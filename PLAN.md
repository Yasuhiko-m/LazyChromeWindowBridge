# LazyChromeExtension PLAN

- Current Version: `V1`
- Current Milestone: `M005`
- Milestone Goal: Caller-controlled per-session monitoring and debugger-notice UX suppression.
- Current Revision: `R011`
- Current Baseline: `V1-M005-R011`
- Milestone Status: **Complete**
- Version Status: **In Progress**

## Handoff

Chat accepted `V1-M004-R010` (continuous-monitor-preview) and `V1-M005-R011`
(session-monitor-control). M004 and M005 are Complete; V1 remains In Progress and
V0 remains Complete. Revisions.md establishes V1-M005-R011 as Current VMR.
Current accepted Source contains both behaviors. Checkpoint subject:
feat: establish V1-M005-R011 caller-controlled monitoring.
Exact checkpoint SHA and remote verification are recorded by Git and the transient handoff.
Existing public GitHub v0.1.0 and NuGet 0.1.0 are older published baselines and
contain neither R010 nor R011. Chrome Web Store live submission remains separate.
PARK/RESTORE controls native placement only; monitoring policy belongs to the Caller.
The third-display initial offscreen capture timeout remains unresolved.
The --silent-debugger-extension-api launch flag is best-effort suppression only;
visual infobar absence remains unverified. The independent Git checkpoint/push creates
no implementation Revision, package/release version, GitHub tag/Release change or
CWS dashboard operation.

This legacy file mirrors the established baseline; candidate work does not advance it.
