# Store asset review — 0.2.0 candidate

Chat accepted the R012 prepared screenshot and this privacy review. This records
local preparation only, not Store submission or publication. The historical
0.1.0 screenshot and [original asset review](privacy-review.md) remain unchanged.

## Real current SampleCaller screenshot

[screenshot-monitor-0.2.0-1280x800.png](screenshot-monitor-0.2.0-1280x800.png)
is 1280x800 RGB PNG, 358774 bytes.
SHA256: `046762153051BF9C777AA06201331707CA1BCFF6088D2A2FDCA825EA5D71D932`.

Captured on 2026-09-13 from the actual Release/win-x64 self-contained 0.2.0
SampleCaller extracted from the candidate Windows ZIP into a fresh directory.
Chrome for Testing 153.0.8010.36 used an isolated profile and the matching ten-file
Extension 0.2.0 from the extracted bundle. Only neutral local A/B/C fixtures were launched.
Native window sizing produced an actual 1280x800 capture; JPEG decoding to PNG only,
without crop, resize, compositing, text replacement, redaction or fabricated pixels.
The original capture and UI observations remain ignored local evidence.

Observed scene: three Bound sessions, A Visible/Paused with frozen frame101,
B Parked/Live and C Visible/Live at frame120. A stayed101 while peers advanced
from101 to120. Current Pause/Resume and FPS/output-bound controls are visible.
The product itself explicitly says Park/Restore never changes Monitor ON/OFF.
Resume then produced fresh A frame122 while peers reached286. Normal close and
fixture cleanup passed. Screenshot capture does not prove Chrome infobar absence.

Privacy review PASS for these exact pixels: no personal account, ChatGPT/OpenAI
session, credentials, capability tokens, private URL/path, filename or unrelated
desktop content. Loopback fixture URLs and ephemeral session/window IDs are neutral
test identifiers, not credentials. Colored webpage previews and their small labels
are actual neutral fixtures. The cursor/highlight comes from real GUI interaction.
No semantic analysis of user webpage content occurred. Later screenshots need their
own review; this is not a blanket claim that monitored pixels cannot be sensitive.

## Retained original branding

The 128px transparent icon and 440x280 RGB promo were visually reviewed again and
retained byte-for-byte. Original window/bridge geometry and the text "Your windows.
Connected locally." do not encode PARKED-only or automatic monitoring behavior.
There is no Chrome logo, third-party trademark artwork or personal data.
Dimensions/formats match the current [Chrome image guidance](https://developer.chrome.com/docs/webstore/images)
and [listing guidance](https://developer.chrome.com/docs/webstore/cws-dashboard-listing).

| Asset | Dimensions | SHA256 |
| --- | --- | --- |
| Original icon-128.png | 128x128 RGBA | `38D1FC2E230AA76CE6DBBDB1123FA108F149DAC2D200331B3AC9D2D5D4233545` |
| Original promo-small-440x280.png | 440x280 RGB | `F2B34175479FE099B724B11386E20E659CAF28DB20CDAE499F1156A19C38B7AA` |

The 16/32/48 icons are also unchanged; their original review/hashes remain authoritative.
No live dashboard action, field edit or upload was performed for this preparation.
