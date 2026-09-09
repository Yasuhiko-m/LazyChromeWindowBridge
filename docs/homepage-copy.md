# Homepage copy

Text for a separate site to reuse later. This file does not deploy or modify a website.

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
V3 thumbnail monitoring runs only for PARKED windows; Visible windows remain ACTIVE
without capture. Read-only Chrome downloads API events report Created, Complete and
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
- PARKED-only human monitoring: default 2 fps, maximum 240×135 JPEG thumbnails.
- Profile-global Chrome download lifecycle; bounded MV3 recovery and retry deduplication.
- .NET Windows Core library, Manifest V3 extension and independent WinForms sample.
- Windows-only, broad debugger/downloads permissions and documented early-release limits.
