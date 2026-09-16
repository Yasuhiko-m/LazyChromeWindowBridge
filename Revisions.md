# Revisions

- Current VMR: `V1-M011-R022`

## Purpose
Source semantic Revision history and, after governance migration, Current VMR authority.

Git records actual Source changes when enabled.
CHANGELOG.md is only the current Codex Task handoff.
Workspace Revisions/ is separate Controller-owned rollback/evidence data.

## Rules
- Current VMR identifies the last established Project Revision.
- Advance Current VMR only when the Project accepts the Revision as established.
- Do not duplicate full Git diffs when Git is authoritative.
- Record why the Revision existed, what became true, build/test evidence, remaining issues, and optional Git commit.
- Same-purpose retries use suffixes such as R001_1.

## History

### V1-M011-R022 — v0.3.1-publication-closeout

Purpose / Result:
Accepted documentation-only closeout for the completed v0.3.1 publication. GitHub
tag/Release `v0.3.1` was published from checkpoint
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd` as a non-draft, non-prerelease release
at 2026-09-16T02:43:20Z: https://github.com/Yasuhiko-m/LazyChromeWindowBridge/releases/tag/v0.3.1.
Published assets are the Extension ZIP (34,137 bytes,
`BEE26DEF8F958934DA7305E4D76B08A3C5FEBB193B8AC284999745B91EB62999`) and Windows
x64 ZIP (95,128,807 bytes,
`C065D57426696C8A338F7A4FC62D099994A78F8B8E04995ED50FCAD116A131AD`).

GitHub Actions `Publish NuGet` run `35049074171` completed successfully, including
exact checkout verification, SDK/Node setup, Test-NuGet, OIDC login and verified Core
package push. The checkpoint-built nupkg is 134,268 bytes
(`7A09659E182620A1F77965ADB04D0ED18E2A7F3821B545CD93CFBC8066E2D5A2`) and the
snupkg is 60,507 bytes (`21FAC31D39AD8DA54845843253D5EC4EADA1D870EFBFDBF39DD4FA53BFF4B6FE`),
both identifying that checkpoint. Gallery indexing/visibility was not independently
measured, so successful workflow/push is the recorded boundary. CWS 0.3.1 was not
operated or published; the submitted CWS 0.2.0 review remains untouched.

Build / Test:
Revision Journal records documentation-only delta
`796aff16cd12e131c846ee1f667c6820da6619c8` to
`d28b673b0a4393d402b09f9d1cfded95a368faf3` at unchanged HEAD/upstream
`6ad192376f5d36ddc7d93bf7eba1dae11cd487bd`. The exact ten-file documentation delta
is `README.md` plus nine `docs/` files:

- `README.md`
- `docs/architecture.md`
- `docs/chrome-web-store.md`
- `docs/github-release.md`
- `docs/homepage-copy.md`
- `docs/integration.md`
- `docs/limitations.md`
- `docs/nuget.md`
- `docs/releases/v0.3.1.md`
- `docs/testing.md`

Scoped whitespace and stale-publication wording checks PASS; index remained empty and
protected hashes remained unchanged. No runtime, API, version, build, test, workflow,
artifact, publication, or remote change occurred in R022.

### V1-M011-R021 — sdk-servicing-line-v031

Purpose / Result:
Accepted release-toolchain policy update: stable .NET SDK 10.0.4xx servicing-line
selection with global.json floor 10.0.401, `rollForward: latestPatch` and
`allowPrerelease: false`. Product runtime, API and Source version 0.3.1 remain unchanged.
The Windows host resolved SDK 10.0.401 and PowerShell 7.6.5.

Build / Test:
Test-NuGet.ps1 PASS: Release build 0 warnings/errors; Core 199, PublicApi 39 and
Extension 47 PASS; package audit and local-feed consumer PASS. Package-Extension.ps1
PASS (34,137 bytes); Package-Windows.ps1 PASS (95,128,795 bytes), including
inventory/extraction. NuGet nupkg is 134,262 bytes and snupkg 60,515 bytes. These are
PRE-CHECKPOINT artifacts only; final publication assets must be rebuilt from the accepted
checkpoint. Test-All.ps1 -Clean passed build/Core/PublicApi/Extension/source audit/extension
packaging; its public-release audit failed only on protected inherited untracked
src/LazyChromeWindowBridge.Extension.zip, which R021 did not alter. Per Project policy,
that inherited issue does not invalidate R021. No isolated CfT executable was available,
so real-browser acceptance was not run.

Historical R018 exact-10.0.400 evidence remains historical and is not rewritten. Next
boundaries are Controller validate-revision, checkpoint/push, final artifact rebuild,
GitHub v0.3.1 Release and NuGet 0.3.1 publication. CWS remains untouched.

### V1-M011-R019 — restore-validation-map

Purpose / Result:
Accepted infrastructure-only repair restoring the required Starter Pack Controller
validation map at `tests/validation-map.json`. SchemaVersion 1 maps Core source to the
Core and external PublicApi test projects, and Extension source to the managed Core test
harness. The map selects managed test projects only; it does not replace Test-All,
Test-NuGet, Node, package, exact-SDK or real-browser gates. JSON/schema/path validation
and scoped whitespace PASS; HEAD/upstream remained unchanged, index empty and protected
hashes unchanged. No runtime, API, version, release or publication change occurred.

The accepted v0.3.1 Source behavior and R018 release acceptance remain unchanged. The
next boundary is Controller validate-revision, then checkpoint/push/publication.

### V1-M011-R018 — v0.3.1-release-preparation

Purpose:
Establish the accepted 0.3.1 Source baseline after the M009 launch-policy work, M010
capture-pipeline and preview-stability work, and M011 release preparation. This closeout
records Source authority only; checkpoint, push and public publication remain separate.

Result:
Accepted by Chat. V1 M009, M010 and M011 are Complete; V1 remains In Progress.
Source version 0.3.1 is the established baseline. R015 adds caller-controlled ordered
Chrome switches, the default `--disable-backgrounding-occluded-windows` policy and its
`PreserveBackgroundRendering` opt-out. R016/R016_1 establish CaptureRegion,
CaptureResize/CaptureResizeFilter and CaptureSourceSize, natural BrowserViewport PNG
capture without CDP clip/scale or Emulation resizing, extension-side region/resize before
final JPEG transport, and exact-HWND NativeWindow GPU crop/resize before bounded readback.
BrowserViewport supports all three filter policies; NativeWindow accepts Bilinear and
rejects NearestNeighbor/Bicubic rather than silently substituting. R017 makes latest-frame
and monitor-state polling observational and preserves exact-owned Visible/Parked monitor
liveness through transient current-bounds unreadability. R018 completes release-ready
documentation, package/release consistency and pre-checkpoint package preparation.

Build / Test:
Exact SDK 10.0.400 Release build PASS with 0 warnings/errors. Core 199 PASS, external
PublicApi 39 PASS and Extension 47 PASS. Source security/privacy audit and scoped
whitespace check PASS. Direct NuGet pack/inventory PASS: Core-only package, both TFMs
and packaged README verified.

Accepted PRE-CHECKPOINT VALIDATION ARTIFACTS only (SHA256):

| Artifact path | SHA256 |
| --- | --- |
| artifacts/nuget/0.3.1/LazyChromeWindowBridge.Core.0.3.1.nupkg | DAF60258E987B9232B76185E92288A48AA3801C8CA440A8CE4157E448C6484B9 |
| artifacts/nuget/0.3.1/LazyChromeWindowBridge.Core.0.3.1.snupkg | 86D6DD1ABAAAA7FECCA7E06DCA5D13035AA94F2DEC1130EA643A78CFEA50155A |

These are validation bytes only, not final publication bytes. PowerShell 7 was unavailable
in R018, so authoritative PowerShell release scripts, Extension/Windows bundle generation,
full NuGet local-feed audit and real Chrome/CfT acceptance were not run there. The user
separately authorized proceeding to public 0.3.1 publication.

Remaining:
Git checkpoint/push, GitHub v0.3.1 Release and NuGet Core 0.3.1 publication are subsequent
Controller boundaries and are not claimed complete. CWS 0.3.1 is outside this release
boundary; the historical submitted CWS 0.2.0 review remains untouched. The known
third-display initial offscreen BrowserViewport timeout and Chrome-dependent silent
debugger suppression limitation remain explicit.

### V1-M008-R014 — v0.3.0-distribution-publication

Purpose:
Prepare the accepted 0.3.0 runtime for GitHub v0.3.0 and NuGet Core 0.3.0 publication,
with a Core-only NuGet package, matching Extension delivery from GitHub Release when
Chrome Web Store review lags, exact release artifacts and publication-safe validation.

Result:
Accepted by Chat. V1 M008 Complete; V1 remains In Progress. Runtime behavior remains
the accepted V1-M007-R013 contract; R014 changes release documentation, NuGet publication
workflow and package verification only. NuGet contains LazyChromeWindowBridge.Core only.
Users use the matching Chrome extension; when the Chrome Web Store does not yet provide
0.3.0, the GitHub v0.3.0 Extension ZIP is extracted and loaded through chrome://extensions
with Developer mode / Load unpacked. Matching Core and Extension versions are recommended.
The submitted CWS 0.2.0 review is historical and untouched; no CWS 0.3.0 publication is
claimed by this Revision.

Build / Test:
Windows validation with exact SDK 10.0.400 PASS. Release build completed with zero
build errors; Core 179, external PublicApi 35 and Extension 40 checks PASS. The isolated
local-feed 0.3.0 PackageReference consumer passed, including both CaptureMode values and
SetShowInTaskbar. NuGet package audit confirmed both target frameworks, portable symbols,
repository provenance, packed README distribution guidance and no Chrome extension
payload in the nupkg. Deterministic Extension packaging PASS and repeated bytes matched.
Self-contained Windows x64 package inventory/extraction PASS.

Accepted PRE-CHECKPOINT VALIDATION ARTIFACTS only (bytes / SHA256):

| Artifact path | Bytes | SHA256 |
| --- | ---: | --- |
| artifacts/cws/LazyChromeWindowBridge.Extension-0.3.0-cws.zip | 28535 | 6B7446564C1F6751BB8731135429C0DEA855EB837E1E2EAC546507D588FD70CB |
| artifacts/release/0.3.0/LazyChromeWindowBridge-v0.3.0-win-x64.zip | 95046058 | AA5C96600321E3ADE23773091AE967867545040FA81F87E0D039B47A12F544CA |
| artifacts/nuget/0.3.0/LazyChromeWindowBridge.Core.0.3.0.nupkg | 121896 | AE7EB75F0B4A038C6C423858E6556D7DC265E8BCFA01A31EC7CAD8B98BBA3068 |
| artifacts/nuget/0.3.0/LazyChromeWindowBridge.Core.0.3.0.snupkg | 57962 | 08986BFBA8CF6A39C6086405A6D8538B214B1ABA45C6D0350410D908F968F73E |

These bytes were generated before the R014 checkpoint, so final publication artifacts
must be rebuilt from the exact accepted checkpoint. In particular, final NuGet
RepositoryCommit/Source Link must identify that checkpoint rather than R013 commit
991457ec6fa8692c3ab0b5ccd7b2c8b2f0ccf30d.

Remaining:
Git checkpoint/push and the explicitly authorized GitHub v0.3.0 / NuGet 0.3.0 publication
are independent Controller boundaries after this acceptance. The known third-display
initial offscreen BrowserViewport timeout remains unresolved. Silent debugger suppression
remains Chrome-dependent best effort. CWS 0.2.0 review remains untouched.

Git Commit:
Acceptance occurred with HEAD at the R013 checkpoint
991457ec6fa8692c3ab0b5ccd7b2c8b2f0ccf30d. The independent R014 checkpoint/push follows
this authority closeout and is recorded by Git when completed.

### V1-M007-R013 — native-window-capture-taskbar

Purpose:
Establish LazyChromeWindowBridge 0.3.0 runtime behavior for an additive exact-HWND
NativeWindow monitor backend and independent per-owned-window taskbar visibility,
while preserving BrowserViewport and all accepted ownership/placement/monitor/download contracts.

Result:
Accepted by Chat. V1 M007 Complete; V1 remains In Progress. `CaptureMode.BrowserViewport`
remains the default and source-compatible 0.2.0 path. `CaptureMode.NativeWindow` captures the
validated owned HWND through Windows Graphics Capture `CreateForWindow`, performs crop/resize
with D3D11 before bounded CPU readback, and encodes JPEG quality70 without a picker, candidate
window list, replacement-window discovery, CDP screenshot capture or BrowserViewport fallback.
`SetShowInTaskbar(Guid,bool)` is an independent exact-HWND runtime policy; it is idempotent,
does not activate/move/resize/rebind or alter monitoring, survives PARK/RESTORE, isolates peers,
and restores the original extended style on normal disposal while the owned window remains live.
PARK/RESTORE continues to control native placement only; monitoring policy remains Caller-owned.
Source version authorities are 0.3.0. Publication is not part of this Revision.

Build / Test:
Accepted complete CfT 155.0.8058.0 validation: build 0 warnings/errors; Core 179, external
PublicApi 35 and Extension 40 checks PASS; source/public audits, downloads, ownership, navigation,
same-URL concurrency, geometry/PARK/RESTORE, BrowserViewport, NativeWindow/taskbar, MV3 recovery,
global Start/Stop and shutdown PASS. Native composition measured 1266x793 versus BrowserViewport
1264x649, proving non-client native extent; NativeWindow issued zero CDP captures. One default
NativeWindow session measured 2.140 fps, 6,033 JPEG bytes/s and 99.76 ms mean capture. Five mixed
Visible/Parked sessions measured 7.408 aggregate fps and 20,578 JPEG bytes/s; all produced fresh
bounded 215x135 JPEGs. These are measurements, not throughput SLAs. Pause retained exact frozen
JPEG/Sequence/ReceivedAt, peers continued, PARK/RESTORE did not auto-resume, resume became fresh,
and shutdown reached zero capture resources/connections while restoring taskbar state and geometry.
Final NuGet validation PASS for both TFMs and isolated local-feed consumer. Evidence:
Scripts/Outputs/20260915-121243432-Test-All.log,
Scripts/Outputs/20260915-121613144-R013-NativeWindow.log and
Scripts/Outputs/20260915-121652535-Test-NuGet.log.

Remaining:
Explorer taskbar-button pixels were not separately inspected; accepted evidence covers exact-HWND
style transitions, successful native refresh, peer isolation and disposal restoration, so no visual
pixel certainty is claimed. The known third-display initial offscreen BrowserViewport timeout remains
unresolved. Silent debugger suppression remains Chrome-dependent best effort. Git checkpoint/push,
GitHub/NuGet 0.3.0 publication and any Chrome Web Store operation are separate boundaries. Historical
GitHub/NuGet 0.2.0 publication and the submitted CWS 0.2.0 review remain unchanged.

Git Commit:
Acceptance occurred with HEAD still at the V1-M006-R012 checkpoint. The independent R013 checkpoint
and push follow this authority closeout and are recorded by Git when completed.

### V1-M006-R012 — v0.2.0-distribution-preparation

Purpose:
Prepare one coherent LazyChromeWindowBridge 0.2.0 GitHub Release, NuGet and Chrome
Web Store distribution candidate from the accepted R011 runtime baseline.

Result:
Accepted by Chat. V1 M006 Complete; V1 remains In Progress. Core NuGet package,
SampleCaller and Extension manifest version authorities are 0.2.0. This is a feature
release for the accepted R010/R011 capabilities: continuous Visible/Parked JPEG,
in-place CaptureOptions updates, requested FPS 1–30, SetSessionMonitoring(Guid,bool),
per-session Paused/frozen JPEG and best-effort silent debugger launch behavior.
PARK/RESTORE controls native placement only and NEVER automatically starts, stops,
pauses or resumes monitoring. Monitoring policy belongs to the Caller. Downloads
and the human-view-only/no-DOM boundary remain. No runtime behavior changed in R012.

Core metadata audit accepted: exact PackageId LazyChromeWindowBridge.Core/version
0.2.0, author matching the Core project metadata, MIT, README, reviewed icon and
project/repository URLs; net10.0-windows7.0 with Microsoft.AspNetCore.App and
Microsoft.WindowsDesktop.App framework references, no external NuGet dependencies,
portable PDB, Source Link and strict nupkg/snupkg inventory. Trusted Publishing stays
manual workflow_dispatch only, environment release, contents:read/id-token:write,
OIDC NuGet login, no long-lived API key and no skip-duplicate behavior. R012 changed
only the exact 0.2.0 package path in the existing workflow; no dispatch occurred.

Initial public CWS candidate is 0.2.0, MV3, with exactly bindings.js, bootstrap.js,
downloads.js, icons/icon-16.png, icons/icon-32.png, icons/icon-48.png,
icons/icon-128.png, manifest.json, monitor.js and service-worker.js. No remote code,
credentials or development fixtures. The real current 1280x800 RGB Store screenshot
and its privacy review are accepted; original promo and icons are retained.
GitHub v0.2.0 is the prepared next release, not an existing tag or public Release.

Build / Test:
Accepted clean restore/build PASS, 0 warnings / 0 errors; Core 154, PublicApi 29,
Extension 39 PASS. Isolated local-feed 0.2.0 PackageReference consumer: 29 checks
including SetSessionMonitoring PASS. Full real Chrome/native ownership, navigation,
same-URL concurrency, native geometry, PARK/RESTORE, continuous monitoring,
per-session Pause/Resume, global Start/Stop, downloads, MV3 and shutdown PASS.
Live options produced 640x329 JPEG with stable generation/socket/attachment;
30 fps request measured about 15.99 fps; five-session aggregate about 9.99 fps.
These measurements establish no throughput SLA. Windows self-contained x64 package
inventory/extraction and fresh-path GUI launch/normal close PASS; final repack changed
only README, with all executable/runtime/Extension bytes identical to the tested ZIP.
CWS repeated deterministic generation, package/source/privacy/link audits PASS.
Evidence under Scripts/Outputs: 20260913-230604104-Test-All.log,
20260913-231210165-Test-NuGet.log, 20260913-231250634-Package-Windows.log,
20260913-225336324-R012-Gui.log and 20260913-231238288-R012-FinalAudit.log.

Accepted PRE-CHECKPOINT VALIDATION ARTIFACTS only (bytes / SHA256):

| Artifact path | Bytes | SHA256 |
| --- | ---: | --- |
| artifacts/nuget/0.2.0/LazyChromeWindowBridge.Core.0.2.0.nupkg | 54321 | 14552205B34925F915B1C34140FE17A5CF35595DC76E2F1192B8C92F98B14803 |
| artifacts/nuget/0.2.0/LazyChromeWindowBridge.Core.0.2.0.snupkg | 25520 | 1383B52B5E36D72E736DA1910792D0E2EB16751CF4E62F65357222FFF2721D59 |
| artifacts/release/0.2.0/LazyChromeWindowBridge-v0.2.0-win-x64.zip | 88526591 | E402CDBBD7D41F601F3CC16717F03E43067C51D22B1E2DF0A0C5B11EBC34F957 |
| artifacts/cws/LazyChromeWindowBridge.Extension-0.2.0-cws.zip | 28408 | 92A4486C7F5CBC7E55933863EFA1F3837FF7258B1DEC64804E30481975473F57 |
| docs/store-assets/screenshot-monitor-0.2.0-1280x800.png | 358774 | 046762153051BF9C777AA06201331707CA1BCFF6088D2A2FDCA825EA5D71D932 |
| docs/store-assets/promo-small-440x280.png | 4718 | F2B34175479FE099B724B11386E20E659CAF28DB20CDAE499F1156A19C38B7AA |
| src/LazyChromeWindowBridge.Extension/icons/icon-128.png | 1411 | 38D1FC2E230AA76CE6DBBDB1123FA108F149DAC2D200331B3AC9D2D5D4233545 |

These packages/archives came from the uncommitted R012 tree. Their hashes are NOT
final publication hashes. After the accepted checkpoint is created and pushed,
publication artifacts must be rebuilt from that exact checkpoint. Final NuGet
repository/Source Link metadata must identify that publication checkpoint rather
than the older 03a8bf92a0808071a28d8143f81346b77216e2da base. Source screenshot/icon/
promo hashes may remain valid if their actual bytes do not change.

Remaining:
Third-display initial offscreen capture timeout remains unresolved; it did not fail
the accepted R012 full run, but no fix is claimed. Silent debugger suppression is
Chrome-dependent best effort; visual infobar absence is not guaranteed. GitHub
v0.1.0 and NuGet 0.1.0 remain historical and unchanged. R012 is accepted/prepared but
established as a distribution candidate but unpublished. CWS 0.2.0 is not uploaded/submitted/approved or
published; the live item remains a separate interactive publication operation.

Git Commit:
Checkpoint subject: build: establish V1-M006-R012 v0.2.0 distribution candidate
Exact checkpoint SHA and remote verification are recorded by Git and the transient
checkpoint handoff. The independent Git checkpoint does not create a new Revision.

### V1-M005-R011 — session-monitor-control

Purpose:
Add Caller-controlled per-session monitoring alongside global batch controls and
best-effort Chrome debugger-notice suppression.

Result:
Accepted by Chat. V1 M005 is Complete; V1 remains In Progress. The public
SetSessionMonitoring(Guid appSessionId, bool enabled) API pauses/resumes one live
owned session without changing peers. Pause detaches capture and stops frame traffic,
retains the waiting WebSocket and exact last JPEG/Sequence/ReceivedAt, and reports
Paused rather than Live. Resume reuses that socket for fresh capture. Global Start
while enabled updates options without resuming paused sessions; global Stop clears
all JPEGs and stops capture, and the next Start batch-enables all live sessions.
PARK/RESTORE does not automatically toggle monitoring. Caller owns monitoring policy;
Visible/Paused, Visible/Live, Parked/Paused and Parked/Live are all valid combinations.
LCWB-launched Chrome includes --silent-debugger-extension-api exactly once as
best-effort UX suppression. Permission/security boundaries remain unchanged, and
production debugger commands remain Page.getLayoutMetrics and Page.captureScreenshot.

Build / Test:
Accepted clean build: 0 warnings / 0 errors; Core 154, PublicApi 29 and Extension 39
PASS. Five-session real pause/resume: A retained Sequence13, 240x123, 1,155 bytes,
ReceivedAt 2026-09-13T12:45:33.9935705Z and SHA256
3EF01F88AAA04DE30E5A878C7CD3BD971AF1AA29E898DEE1DC7587FDE7C7C3C9.
Pause attach1/detach1; resume attach2/detach1, waiting socket1 reused, fresh Sequence14.
Peers remained fresh with stable generations/sockets/attachments. Pause survived
Park/Restore and global options updates. Global Stop: Connections5, CapturingConnections0,
all latest=null; batch Start reused sockets; Dispose reached Connections0.
R010 placement regression: generation5/socket2/attach1/detach0 PASS. Full ownership,
navigation, same-URL, native geometry, downloads, MV3 and shutdown regressions PASS.
SampleCaller pause/frozen/placement/resume/global restart GUI PASS. Real OS command
lines confirmed the silent flag exactly once. Evidence: 20260913-214150423-Test-All.log
and 20260913-214647406-Test-All.log under Scripts/Outputs.

Remaining:
Third-display initial offscreen capture timeout remains unresolved. Silent flag
suppression is best-effort; visual infobar absence was not established in acceptance.
Chrome may ignore the flag; already-running profiles may retain original flags.
Accepted Source contains R010/R011. Existing GitHub v0.1.0 and NuGet
0.1.0 remain older baselines; CWS live submission is separate.

Git Commit:
Checkpoint subject: feat: establish V1-M005-R011 caller-controlled monitoring.
Exact checkpoint SHA and remote verification are recorded by Git and the transient
closeout handoff. This checkpoint includes accepted R010 and R011 together.

### V1-M004-R010 — continuous-monitor-preview

Purpose:
Use one continuous human-view JPEG monitor path for live owned Visible and Parked
windows, independently of native placement, with in-place capture options.

Result:
Accepted by Chat. V1 M004 is Complete. Both placements use Page.captureScreenshot;
placement-only transitions preserve monitor generation, WebSocket identity, latest
JPEG and same-tab debugger attachment. StartMonitoring(options) while enabled updates
in place. FPS requests accept 1–30, default2; default output bounds240x135, quality70,
aspect preserved and no upscale. Bounds affect JPEG only, never native size, viewport
or zoom. Latest-frame-only semantics remain. Stop stops capture, detaches debugger and
clears JPEGs while retaining restart-control sockets; Dispose closes those sockets.

Build / Test:
Accepted clean build: 0 warnings / 0 errors; Core 129, PublicApi 24 and Extension 37
PASS. Visible fresh JPEG PASS. Visible→Parked→Visible→Parked: Sequence23→33→54,
generation5 and WebSocket object2 unchanged, debugger attach1/detach0.
In-place 15fps/640x360 yielded 640x329 JPEG with the same generation/socket/native
bounds/viewport. A 30fps request measured 17.076fps without transport error; exact
throughput is not an SLA. All five mixed-placement sessions were eligible at about
9.965 aggregate fps. Stop/restart/Dispose, all session/native/download/MV3 regressions
and SampleCaller continuous-preview acceptance PASS.

Remaining:
Third-display initial offscreen capture timeout remains unresolved and is not claimed
fixed. No timeout/assertion was weakened. Short measured throughput is not a guarantee.
Accepted Source is newer than published GitHub v0.1.0 and NuGet 0.1.0.

Git Commit:
Checkpoint subject: feat: establish V1-M005-R011 caller-controlled monitoring.
Exact checkpoint SHA and remote verification are recorded by Git and the transient
closeout handoff. This checkpoint includes accepted R010 and R011 together.

### V1-M003-R009 — official-package-distribution

Purpose:
Establish official Core NuGet and Chrome Web Store distribution preparation while
preserving the accepted runtime and the already-published GitHub v0.1.0 baseline.

Result:
Accepted by Chat. M003 Official Package Distribution is Complete. Core 0.1.0 has
MIT/readme/project/repository/tags/icon metadata, portable symbols and an isolated
local PackageReference consumer gate. SDK 10.0.400 and publishing actions are pinned.
The manual-only main-branch publish-nuget.yml uses environment release and OIDC with
contents:read/id-token:write; no long-lived API-key secret or duplicate suppression.
Chrome Web Store 0.1.0 preparation includes original transparent 16/32/48/128 icons,
manifest icons without a toolbar action, actual 1280x800 SampleCaller screenshot,
original 440x280 promo, public privacy policy, permission/privacy/reviewer copy and
a deterministic ten-file ZIP. No production C# or extension JavaScript changed.

Build / Test:
20260909-215716864-Test-NuGet.log: Release build 0 warnings/errors; 121 Core,
19 public API, 33 extension checks; exact package metadata/icon/symbol audit and
19 local-feed consumer checks PASS. actionlint 1.7.12, PowerShell syntax, exact
Trusted Publishing tuple, repository/environment secret inventory and scoped
whitespace/path gates PASS. Altered package icon is rejected by the audit.
20260909-221406063-Test-All.log: clean standard primary-display full validation,
0 warnings/errors, all existing deterministic tests, real download/session/native
geometry/monitor/five-session regressions, source/privacy/image/link/ZIP audits,
Exit 0. Every browser mode loads the exact CWS ZIP extraction; entry hashes match.
Actual third-display isolated GUI: one ACTIVE plus four PARKED/LIVE and normal close.
CWS ZIP: 28042 bytes, repeated identical SHA256
A20C7781501F2DC43C8133D9C3F0EAD165E5E94BE6F4C0B3CACAACC3CED52A31.
Store screenshot: 1280x800, SHA256
1C689BFBBB228286D409CE0ACE7CFD686F48CB0BED3C2B15D266B6A07B2C8D2B.

Remaining:
Third-display automatic monitor runs 20260909-220724361-Test-All.log and
20260909-221059687-Test-All.log reproduced the initial PARKED capture timeout.
The root cause is unresolved. Primary-display full validation and third-display
GUI success do not establish a fix. No timeout, assertion or runtime was weakened.
Existing privacy/capacity/DPI/restart limitations remain. NuGet dispatch/publication
is explicitly authorized only after checkpoint/push/preflight; its outcome is
recorded in the closeout handoff. CWS upload/item creation/submission remains deferred.
No GitHub v0.1.0 tag/Release change or further runtime implementation is authorized.

Git Commit:
Checkpoint subject: build: establish V1-M003-R009 official package distribution.
Exact SHA and remote/publishing verification are recorded by Git and the closeout report.

### V1-M002-R008 — public-release-preparation

Purpose:
Prepare the accepted Windows-to-Chrome bridge for an MIT public source release
without changing runtime features or refactoring product code.

Result:
Accepted by Chat. V1 M002 is Complete. Standard MIT license, public README and docs,
architecture/lifecycle diagrams, release notes and a reviewed actual SampleCaller
hero screenshot are established. The approved lower README provenance note states
that Lazy AI Deck was the development orchestration environment and is not a runtime
dependency. The user explicitly authorizes normal push, PUBLIC repository visibility
and published GitHub Release v0.1.0; remote publication follows this checkpoint.

Build / Test:
Final full Test-All -Clean with the accepted Chrome for Testing 153.0.8010.36 passed
on a fresh-profile rerun (20260909-194815023-Test-All.log, Exit 0): clean restore/build,
0 warnings / 0 errors, 121 Core / 19 external public API / 33 extension checks,
session/navigation/native geometry/PARK/RESTORE, monitor, five-session lifecycle,
download Created/Complete/Interrupted, MV3 restart and consumer move after Complete.
Public-release and source audits passed: 24 relative links, reviewed image privacy/
hash/metadata and deterministic six-file root-manifest extension package. Existing
real sample GUI/image acceptance remains applicable; UI and hero pixels are unchanged.
Hero: 1082x552, SHA256
59B5437CA2D7D7D2BED788A045D5FCAB571ECDE14A0D37A06D5EC3DCAED4E21A.
ZIP: 24453 bytes, repeated identical generation, SHA256
131C1E1964001A6C5AD45FBC159251FA69AB3C718E892681DA5F1EB5F5829C22.
Generated ZIP, logs and raw evidence remain uncommitted.

Remaining:
The initial final-validation run (20260909-194338405-Test-All.log, Exit 1) reproduced
the documented initial offscreen capture timeout in one of four PARKED sessions;
three peers remained Live. The unchanged complete rerun passed. No assertion,
timeout or runtime behavior was weakened; this does not establish a root cause or
eliminate the intermittent limitation. Existing support/privacy/download bounds remain.
Publication must stop on GH007, mismatched remote/tag or a public-render defect
requiring another Source edit. No history rewrite or automatic email-privacy change.

Git Commit:
Checkpoint subject: docs: establish V1-M002-R008 public release preparation.
Exact checkpoint and subsequent publication outcomes are recorded by Git and the
publication task handoff. Public accessibility is not claimed by this local record.

### V1-M001-R007 — download-lifecycle-notification

Purpose:
Observe Chrome Download Manager lifecycle through the existing local Bridge without
page inspection, download mutation or guessed application-session attribution.

Result:
Accepted by Chat. BridgeRuntime exposes DownloadChanged, GetDownloads and GetDownload
with exact ID/state/filename/error/observation time. One profile-global stream per
Bridge uses a representative session capability only for authentication. The bounded
storage.session outbox and runtime sequence high-water mark preserve ordering and
deduplicate retries within the existing browser session. Product calls are only
downloads.onCreated/onChanged/search. Consumers own post-Complete filesystem checks.

Build / Test:
Clean restore/build PASS, 0 warnings / 0 errors. 121 Core, 19 external public API and
33 extension checks PASS, preserving all prior assertions. Complete real Chrome
153.0.8010.36 session/native/monitor/five-session regressions PASS. Real local ZIP
Created/Complete, five-session single stream, Interrupted with official
SERVER_CONTENT_LENGTH_MISMATCH, worker restart during slow download and stable/
exclusive consumer move after Complete all PASS. Chrome remained complete after move.
Final filename appeared before Complete but was not used as readiness authority.
Source/name/security audit PASS; protected hashes and inherited diff unchanged.

Remaining:
V1 M001 Complete; M002 Public Release Preparation In Progress. Bounded overflow,
full-browser/caller restart and extension reload/update recovery are not guaranteed.
Callbacks must return promptly; absolute filenames are sensitive. Existing intermittent
initial offscreen capture timeout remains documented; it did not recur in this run.
No runtime file mutation, URL/content collection or new external dependencies.

Git Commit:
Checkpoint subject: feat: establish V1-M001-R007 download lifecycle notification.
Exact checkpoint is recorded in Git and the establishing task handoff.

### LazyChromeExtension_V0-M001-R000 — Starter Pack bootstrap
Purpose:
Materialize the Starter Pack governance baseline before the first project-specific Revision.

Result:
The Project has its initial governance documents and an operational bootstrap VMR.

Build / Test:
No Codex implementation or test Revision is claimed by this bootstrap entry.

Remaining:
Chat defines the first project-specific Revision after creation.

Git Commit:
N/A

### LazyChromeExtension_V0-M001-R001 — project-bootstrap
Purpose:
Establish the first project-specific Source foundation: a minimal Windows C#
CallerHarness, a minimal Chrome Manifest V3 extension, and the adopted project
documentation/roadmap.

Result:
Accepted by Chat. The project now contains `LazyChromeExtension.sln`, Windows
Forms `src/CallerHarness`, minimal permission-free MV3 `src/Extension`, adopted
`PROJECT.md`, `SPEC.md`, and `PLANS.md`, and a local Git Source repository.
Session binding, IPC, geometry, PARK/offscreen behavior, capture, and monitoring
remain unimplemented.

Build / Test:
Accepted implementation evidence (not rerun for this authority-only checkpoint):
- CallerHarness: `net10.0-windows`.
- Restore: PASS.
- Build: PASS, 0 warnings / 0 errors.
- GUI smoke launch/respond/normal-close: PASS.
- MV3 JSON validation: PASS.
- Service-worker JavaScript syntax validation: PASS.
- Chrome unpacked-extension load remains a manual acceptance check.

Remaining:
M001 continues. The next Revision is not predefined here.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M001-R002 — session-window-binding

Purpose:
Establish deterministic caller-session ownership of launched Chrome windows
independently of subsequent page navigation.

Result:
Accepted by Chat. CallerHarness owns the session identity and loopback bridge;
the extension binds the exact sender Chrome Window. The launch URL remains immutable
session metadata, and later navigation never redefines ownership. Simultaneous
same-URL sessions remain distinct. Window close terminates only its own session;
normal MV3 service-worker restart rehydrates active bindings. Geometry, PARK, and
monitoring are not part of R002.

Build / Test:
Accepted evidence: .NET build PASS (0 warnings / 0 errors); caller/HTTP checks
26 PASS; extension tests 6 PASS; Chrome for Testing integration PASS; one-session
navigation PASS; concurrent same-URL sessions PASS; Window-close PASS; MV3 worker
stop/restart PASS; Windows GUI acceptance PASS.

Remaining:
M001 is complete. M002 begins with geometry / PARK / RESTORE.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M002-R003 — geometry-park-restore

Purpose:
Complete launch-profile geometry persistence and exact PARK / RESTORE for the
bound Chrome Window.

Result:
Accepted by Chat. Geometry belongs to the original launch URL and normal placement
survives navigation. Exact HWND mapping is established before leaving the caller's
bootstrap. Native PARK moves that Window outside every active monitor; logical
PARK state prevents normal geometry contamination. RESTORE returns the same Window
to remembered normal placement. Concurrent sessions remain isolated and topology
fallback is deterministic. Monitoring remains unimplemented.

Build / Test:
Accepted evidence: build PASS (0 warnings / 0 errors); 57 caller checks PASS;
8 extension checks PASS; real negative-coordinate hardware PASS; geometry
save/relaunch PASS; PARK isolation PASS; navigation while PARKED PASS; RESTORE
PASS (maximum measured error 0 pixels); relaunch after PARK PASS; GUI acceptance
PASS. Mixed-DPI and monitor-removal cases have deterministic simulated coverage,
not physical hardware evidence. M001 regressions remain passing.

Remaining:
M002 is complete. M003 begins.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M003-R004 — human-monitor-integration

Purpose:
Provide a practical human-only live preview of the exact PARKED owned WebApp window
and validate the complete launch/binding/geometry/monitor/restore lifecycle.

Result:
Accepted by Chat. Chrome debugger viewport JPEG capture and authenticated loopback
WebSocket transport provide fresh images while the exact native window is fully
outside every monitor. Session/window/native identity and selection generations
prevent cross-session pixels. Navigation, PARK/RESTORE and MV3 recovery preserve
ownership. Normal caller shutdown detaches capture and restores parked geometry.
The accepted default is 2 fps / maximum 960×540 / JPEG quality 70. Chrome debugger
permission and its normal debugging notice are an accepted tradeoff. Product image
handling is pixels-only: no DOM/OCR/semantic/output extraction or page decisions.
Native PrintWindow was rejected after empirically freezing while PARKED.

Build / Test:
Build PASS, 0 warnings / 0 errors; 73 caller checks and 14 extension checks PASS;
all R002/R003 regressions PASS; real fully offscreen changing frames, A/B isolation,
parked navigation, same-HWND RESTORE (measured 0 px), MV3 worker recovery, capture
cleanup and actual WinForms GUI acceptance PASS. Four practical FPS/resolution
configurations were measured. Mixed-DPI/monitor removal remain simulated coverage;
normal authenticated Chrome/ChatGPT smoke was skipped without accessing that profile.

Remaining:
M003 complete. M004 final runtime tuning/multi-window/manual geometry begins.
M005 productization/rename/refactor is roadmap only and has no Revision assigned.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M004-R005 — parked-monitor-control

Purpose:
Align the reference runtime with one Visible ACTIVE window and four independently
monitored PARKED windows, reduce thumbnail load and establish manual native bounds.

Result:
Accepted by Chat. Global monitoring captures only eligible PARKED sessions with
independent connections/generations/native identity checks. Visible windows have
zero image traffic and no monitoring debugger attachment. The default is 2 fps,
maximum 240×135, JPEG quality 70, aspect-preserving without capture upscale.
Native PARK shrinking was measured and rejected; production preserves full native
size and protected Normal geometry. Windows-side Get and Visible-only physical-
pixel SetWindowBounds support exact ownership and reachable negative coordinates.
Restore/re-PARK, tab change/close, MV3 recovery and shutdown preserve isolation.

Build / Test:
Build PASS, 0 warnings / 0 errors; 96 caller checks and 17 extension checks PASS;
R002/R003/R004 regressions, five-session real native/browser acceptance and WinForms
GUI PASS. Four streams remained fresh at about 1.97 fps; ACTIVE had zero frames/
bytes and no debugger. Real negative Set and same-HWND Restore measured 0 px error.
Shutdown detached all targets/connections and restored Normal with 0 px error.
Product remains human-view-only, with no DOM/OCR/semantic/output extraction.

Remaining:
M004 complete. M005 productization/refactoring begins as R006 candidate. Short local
measurements and simulated mixed-DPI/topology coverage retain their stated limits.

Git Commit:
Recorded externally by Git checkpoint; see repository history.

### LazyChromeExtension_V0-M005-R006 — productize-refactor

Purpose:
Establish LazyChromeWindowBridge as an independent reusable Windows-to-Chrome
product while retaining the accepted R005 runtime behavior and immutable ProjectID.

Result:
Accepted by Chat as the final V0 Source Revision. Solution, projects, directories,
namespaces, runtime markers, bootstrap paths and extension/sample display identity
use LazyChromeWindowBridge. Core provides the public BridgeRuntime facade and
encoded frame metadata/bytes without WinForms/WPF UI type references. The separate
WinForms SampleCaller uses only that public API; the MV3 extension retains exact
session/window ownership and PARKED-only capture. Stable Test-All validation and
architecture/integration/security/limitations documentation are established.
Defaults remain 2 fps, maximum 240x135, JPEG70; native shrinking remains rejected.

Build / Test:
Clean restore/build PASS, 0 warnings / 0 errors. 96 Core checks, 17 independent
public API checks and 17 extension checks PASS. Complete real-browser/native
regressions and five-session public-API GUI acceptance PASS. Four PARKED streams
updated independently at approximately 1.98-1.99 fps; Visible remained ACTIVE with
zero frames/bytes and no capture debugger attachment. Negative physical placement,
same-HWND RESTORE and normal-shutdown restoration measured 0 px error. Shutdown
left zero monitor connections/captures and detached all owned debugger targets.
Source audit: 60 files, zero violations; 24 prior residual occurrences explicitly
allowlisted as immutable governance/history. This new history heading is another
intentional ProjectID reference. No runtime old-name aliases or new external
NuGet/npm packages. Protected governance hashes and inherited diff were unchanged.

Remaining:
M005 Complete. V0 Complete. Two earlier initial offscreen captures timed out;
the full final run and third-monitor GUI passed without weakening assertions or
timeouts. The intermittent cause remains unconfirmed and documented. Short neutral
page measurements, physical 96-DPI coverage, simulated mixed-DPI/topology cases,
installed-Chrome consent, full-restart and forced-termination limits remain.
Private GitHub baseline publication is authorized after checkpoint; public release
and licensing are not established. No further implementation is authorized here.

Git Commit:
Checkpoint subject: feat: establish V0-M005-R006 LazyChromeWindowBridge.
Exact commit and publication verification are recorded by Git and the task report.
