# Chrome Web Store submission preparation — 0.1.0

R009 candidate only. Nothing has been uploaded or submitted. Publisher setup was
reported complete by the owner; publisher display name: `yasuhiko-m`. Category: **Tools**.
The URLs below become submission-ready only after the accepted candidate is pushed
and its privacy page is confirmed publicly reachable. No store item URL exists yet.

## Name

LazyChromeWindowBridge

## Summary

Connect a Windows companion to its Chrome windows for local window management and human-viewed parked thumbnails.

## Detailed description

LazyChromeWindowBridge connects a Windows companion application to the Chrome windows
that companion launches. It requires the LazyChromeWindowBridge Windows companion;
installing the extension alone does not provide a standalone dashboard.

From the companion you can launch a URL in an owned Chrome window, save and restore
normal placement, and explicitly PARK or RESTORE that window. With monitoring enabled,
the companion shows live thumbnails of owned PARKED windows for human viewing. Visible
windows show ACTIVE without capture. Chrome may display its debugger notice while
parked thumbnails are enabled.

The bridge also observes Chrome Download Manager lifecycle notifications. These are
profile-wide notifications, not proof that a particular page produced a download.
Absolute filenames and thumbnail pixels can be sensitive. They are handled locally
and sent only to the authenticated companion on the same computer. There is no
telemetry, advertising or cloud relay.

This extension does not automate webpage input, read the DOM, run OCR, inspect network
responses or infer page completion. It has no toolbar popup: use the Windows companion
controls. Windows x64, Chrome 120 or later, and the companion are required. The supplied
self-contained companion needs no separate .NET installation or account sign-in.

Download the companion from the public project homepage's v0.1.0 GitHub Release.
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

Captures JPEG thumbnails only for exact owned PARKED targets after the user enables
monitoring in the companion. Product commands are exactly Page.getLayoutMetrics and
Page.captureScreenshot. This supports live offscreen human viewing across several
parked windows; active-tab capture cannot provide this behavior. There is no DOM,
Runtime.evaluate, network-response inspection, OCR or automated page input. Visible
windows are not captured; restoring/stopping detaches capture. Chrome's debugger
notice is expected and must not be dismissed to force continued monitoring.

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
| Website content | Yes: temporary JPEG pixels of user-selected parked pages. |
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
2. Download `LazyChromeWindowBridge-v0.1.0-win-x64.zip` from the public
   [v0.1.0 Release](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/releases/tag/v0.1.0).
   Extract it and run `SampleCaller/LazyChromeWindowBridge.SampleCaller.exe` inside
   the bundle root. It is self-contained. Use the extension under review; the older
   Extension folder bundled with that historical GitHub Release is not this candidate.
3. Replace the sample's default launch URL with `https://example.com/?window=a`.
   Click Launch and wait for Bound. Repeat with query values b, c, d and e. These
   public neutral pages require no login, SDK, local test server or developer fixture.
4. Select four different session rows and click Park selected for each. Keep the fifth
   visible. Click Start monitor. Expect four PARKED/LIVE thumbnail tiles and one ACTIVE
   tile without capture. The static example page need not animate: the frame counters
   confirm ongoing capture. Allow a few seconds for the first frames.
5. Restore a parked session: its capture stops and the owned window returns to its normal
   placement. Park it again to resume. Stop monitor ends capture for all sessions.
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

Prepared against Chrome's [image requirements](https://developer.chrome.com/docs/webstore/images),
[privacy field guidance](https://developer.chrome.com/docs/webstore/cws-dashboard-privacy)
and [User Data FAQ](https://developer.chrome.com/docs/webstore/program-policies/user-data-faq).
The local-only design still requires a privacy policy. Submission and policy acceptance
remain publisher/Store actions after Chat accepts R009.
