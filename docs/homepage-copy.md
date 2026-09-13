# Homepage copy

Text for a separate site to reuse later. This file does not deploy or modify a website.
This copy describes Chat-accepted R010/R011 Source at V1-M005-R011, not published 0.1.0 binaries.
Version 0.2.0 is a prepared release candidate; do not present it as publicly released.

## One-line tagline
Control the Chrome window, not the webpage.

## Short paragraph
LazyChromeWindowBridge connects .NET Windows applications to session-bound Chrome
windows. Manage native placement, PARK/RESTORE and small human-view thumbnails, and
observe Chrome download lifecycle events without DOM automation.

## Medium feature description
Build Windows Chrome integration around exact ownership: an appSessionId maps to a
Chrome WindowId and a retained HWND/PID/property identity, independent of navigation.
Persist the original launch URL's normal geometry, set physical window position and
size, and PARK a full-size window outside every monitor before restoring it. Manifest
V3 JPEG monitoring runs for both Visible and Parked windows, retaining its generation,
latest preview and same-tab attachment through placement and option changes.
The caller owns monitoring policy: **PARK/RESTORE never automatically switches Monitor
ON/OFF.** Pause one session to retain a frozen last JPEG while peers remain live; Resume
reuses its waiting socket. Global Start/Stop remain batch controls; option updates preserve
explicit session pauses. Global Stop clears previews and the next Start enables all live sessions.
LCWB-launched Chrome uses --silent-debugger-extension-api for best-effort infobar
suppression on supported Chrome. It does not weaken debugger permission; Chrome may
ignore it and show a notice. Production commands remain Page.getLayoutMetrics and
Page.captureScreenshot, independent of whether suppression works.
Read-only Chrome downloads API events report Created, Complete and
Interrupted once per Bridge, with no guessed session attribution. Your consumer owns
the UI and all filesystem checks and moves. Source is available under MIT.

## What it doesn't do
No DOM scraping, Runtime.evaluate, OCR, page-content analysis, automated click/input,
ChatGPT output/completion extraction or network response inspection. Thumbnails are
for humans, and Chrome Complete does not replace consumer filesystem safety checks.

## Link-button suggestion
**Explore LazyChromeWindowBridge on GitHub** →
[repository](https://github.com/Yasuhiko-m/LazyChromeWindowBridge)

Publish that button only once the repository has actually been made public.

## Technical features
- Deterministic Chrome session binding through an authenticated loopback Core API.
- Native Chrome window control using HWND/PID/property validation.
- Physical-pixel geometry, multi-monitor placement and negative coordinates.
- Full offscreen PARK/RESTORE and manual bounds for Visible windows.
- Continuous human monitoring: default 2 fps / 240×135 JPEG, fixed quality70; 1–30 fps requests.
- Profile-global Chrome download lifecycle; bounded MV3 recovery and retry deduplication.
- .NET Windows Core library, Manifest V3 extension and independent WinForms sample.
- Windows-only, broad debugger/downloads permissions and documented early-release limits.
