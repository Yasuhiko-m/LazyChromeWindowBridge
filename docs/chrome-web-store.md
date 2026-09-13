# Chrome Web Store submission preparation — 0.1.0

Future submission copy for Chat-accepted R010/R011 Source, not yet released as a package or submitted to CWS. Publisher setup was
reported complete by the owner; publisher display name: `yasuhiko-m`. Category: **Tools**.
The URLs below become submission-ready only after the accepted Source is pushed
and its privacy page is confirmed publicly reachable. No store item URL exists yet.

## Name

LazyChromeWindowBridge

## Summary

Connect a Windows companion to its Chrome windows for local window management and continuous human-view JPEG previews.

## Detailed description

LazyChromeWindowBridge connects a Windows companion application to the Chrome windows
that companion launches. It requires the LazyChromeWindowBridge Windows companion;
installing the extension alone does not provide a standalone dashboard.

From the companion you can launch a URL in an owned Chrome window, save and restore
normal placement, and explicitly PARK or RESTORE that window. With monitoring enabled,
the companion shows live JPEG previews of owned Visible and Parked windows for human
viewing. Park/Restore and live FPS/output-size changes keep the same preview connection.
Chrome may display its debugger notice in either placement while monitoring is enabled.

The bridge also observes Chrome Download Manager lifecycle notifications. These are
profile-wide notifications, not proof that a particular page produced a download.
Absolute filenames and thumbnail pixels can be sensitive. They are handled locally
and sent only to the authenticated companion on the same computer. There is no
telemetry, advertising or cloud relay.

This extension does not automate webpage input, read the DOM, run OCR, inspect network
responses or infer page completion. It has no toolbar popup: use the Windows companion
controls. Windows x64, Chrome 120 or later, and the companion are required. The supplied
self-contained companion needs no separate .NET installation or account sign-in.

Use a companion built from the same future accepted Source as the extension.
The already published v0.1.0 companion does not contain continuous monitoring.
Prepare and identify a matching companion bundle before any future submission.
Read the privacy policy before monitoring pages that may contain private information.

## Dashboard links

- Homepage: https://github.com/Yasuhiko-m/LazyChromeWindowBridge
- Support URL: https://github.com/Yasuhiko-m/LazyChromeWindowBridge/issues
- Privacy Policy URL: https://github.com/Yasuhiko-m/LazyChromeWindowBridge/blob/main/PRIVACY.md

## Single-purpose statement

Bridge a Windows companion application to its owned Chrome windows so the companion
can manage native window lifecycle and observe limited Chrome application state,
without automating webpage content.

## Permission justifications

### storage

Stores bounded session ownership records, local capability tokens, monitor target IDs
and the pending download-notification outbox in chrome.storage.session. This permits
MV3 worker suspension/restart recovery without inventing ownership or losing pending
events. Access is limited to trusted extension contexts. No sync storage or persistent
download-history database is used.

### alarms

Schedules local bridge reconciliation after MV3 worker suspension, verifies whether
owned sessions still exist, and retries bounded pending lifecycle delivery. This is
required for reliable companion ownership and recovery; it does not schedule telemetry
or background webpage automation.

### debugger

Captures JPEG previews only for exact live owned Visible and Parked targets after the user enables
monitoring in the companion. Product commands are exactly Page.getLayoutMetrics and
Page.captureScreenshot. This supports live offscreen human viewing across several
windows, including offscreen ones; activeTab permission alone cannot provide this behavior.
There is no DOM, Runtime.evaluate, network-response inspection, OCR or automated page input.
Restoring or updating capture options retains the same-tab debugger attachment.
The caller owns monitoring policy: PARK/RESTORE never automatically switches Monitor
ON/OFF. Session pause detaches only that target and retains its last JPEG as a frozen
Paused preview. Resume reuses its waiting socket and obtains fresh frames. Global
methods remain batch controls; global option updates preserve pauses and global Stop
clears all previews, retaining idle restart-control sockets for a later batch Start.
LCWB-launched Chrome requests --silent-debugger-extension-api as best-effort infobar
suppression. This does not weaken debugger permission or justify the permission's safety.
Visual infobar absence was not established in acceptance and must not be claimed.
The extension itself does not remove notices; Chrome may ignore the flag or reuse an
existing process without it. Capture works either way and user cancellation is respected.

### downloads

Observes profile-global Download Manager lifecycle through onCreated, onChanged and
search only. It resolves current lifecycle state and absolute filenames for local
companion notifications. It does not start, cancel, pause, erase, open or read downloads;
it does not collect download URLs or content. Chrome requires this broad permission
for those read-only event/search APIs. Incognito downloads are excluded.

### http://127.0.0.1/*

Connects only to a capability-authenticated Windows companion on IPv4 loopback.
The bootstrap content script matches only the bridge bootstrap path on this host.
The local companion chooses an available port, so permission must cover loopback
ports. It is not access to arbitrary internet sites or a cloud service. Bridge data
remains on the same machine.

## Privacy practices — recommended dashboard answers

Remote code: **No, I am not using remote code.** All extension JavaScript is packaged.
The separate installed companion is not fetched/executed as extension JavaScript.

Do not select a blanket “no user data” answer. Recommended conservative disclosure:

| Data category | Recommended answer and scope |
| --- | --- |
| Web history | Yes: original launch URLs, not a browser-history scan. |
| Website content | Yes: temporary JPEG pixels of monitored owned Visible and Parked pages. |
| Authentication information | Yes: local bridge capability tokens; no website login credential extraction. |
| Personally identifiable information | Yes: absolute filenames or displayed pixels may contain names and identifiers. |
| Personal communications | Yes: a monitored page may display messages in its pixels; no message parsing. |
| Health information | Yes: potentially visible in user-selected page pixels, not separately queried. |
| Financial and payment information | Yes: potentially visible in page pixels, not separately queried. |
| Location | Yes: potentially visible in page pixels; no geolocation API or location tracking. |
| User activity | Yes: limited launch/window/download lifecycle state; no keystroke, click or mouse logging. |

These recommendations disclose possible sensitive pixel content conservatively; they
do not claim dedicated collection APIs for every category. The publisher should review
the dashboard's current definitions before final certification. All of the above is
local feature processing; none is sent to the publisher or sold.

Certify all three limited-use statements: no sale/transfer outside approved purposes;
no unrelated use/transfer; no use for creditworthiness or lending. The
[privacy policy](../PRIVACY.md) explains retention, local HTTP, local companion transfer,
and user controls. These answers do not constitute Store approval.

## Reviewer instructions

1. Use Windows x64 with Chrome 120+ and the extension version 0.1.0 under review enabled.
   No ChatGPT, OpenAI or other website credentials are needed.
2. Use the matching companion build identified for the future submission, and run
   SampleCaller with the extension under review in the same Chrome profile. A new
   distribution decision/bundle is required before these instructions are submission-ready.
   The historical [v0.1.0 Release](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/releases/tag/v0.1.0)
   and NuGet 0.1.0 do not implement the accepted R010/R011 monitor contract.
3. Replace the sample's default launch URL with `https://example.com/?window=a`.
   Click Launch and wait for Bound. Repeat with query values b, c, d and e. These
   public neutral pages require no login, SDK, local test server or developer fixture.
4. Click Start monitor while all five sessions are Visible. Expect five LIVE JPEG
   previews. Park four and keep the fifth visible; all five previews continue.
   The static example page need not animate: the frame counters
   confirm ongoing capture. Allow a few seconds for the first frames.
5. Restore a parked session, then Park/Restore/Park it again: its JPEG preview and
   monitor generation continue while native placement changes. Change FPS and JPEG
   max width/height, then Apply preview without stopping. Bounds affect JPEG output
   only; native size, viewport and zoom stay unchanged. Stop monitor ends all captures
   and clears previews; Start resumes on the retained control connections.
   Before global Stop, select one session and Pause preview: only its JPEG/frame number
   freezes and its state becomes Paused. Peers remain LIVE. Park/Restore that paused
   session and Apply new output options: it stays Paused. Resume preview obtains fresh
   frames for only that session. These are caller controls, independent of placement.
6. Optional download check: manually save a harmless page through Chrome's normal Save
   command. The bridge observes Chrome's lifecycle; it never initiates the download.
   SampleCaller is primarily a placement/thumbnail UI; download events are exposed by
   the companion's Core API, not a separate SampleCaller download panel.
7. Close SampleCaller normally. Parked windows are restored and Chrome remains open.
   If connection fails, verify the extension is in the same Chrome profile used by
   SampleCaller and no other companion instance is running. No account access is needed.

## Submission files

- ZIP: `artifacts/cws/LazyChromeWindowBridge.Extension-0.1.0-cws.zip` (10 files).
- Icon: `src/LazyChromeWindowBridge.Extension/icons/icon-128.png`.
- Screenshot: [real monitor view](store-assets/screenshot-monitor-1280x800.png), 1280×800.
- Small promo: [original brand artwork](store-assets/promo-small-440x280.png), 440×280.
- [Asset privacy review](store-assets/privacy-review.md).

The screenshot above is historical R009 evidence. Prepare a current continuous-monitor
screenshot and repeat its privacy review before a future Store submission; no asset
or dashboard upload is part of this authority closeout.

Prepared against Chrome's [image requirements](https://developer.chrome.com/docs/webstore/images),
[privacy field guidance](https://developer.chrome.com/docs/webstore/cws-dashboard-privacy)
and [User Data FAQ](https://developer.chrome.com/docs/webstore/program-policies/user-data-faq).
The local-only design still requires a privacy policy. Submission and policy acceptance
remain publisher/Store actions after Chat acceptance and separate submission authorization.
