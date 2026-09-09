# Store asset review — R009 candidate

Manual pixel review on 2026-09-09 is bound to the exact SHA256 values below.
Any byte change requires a new review; AuditPublicRelease checks dimensions, PNG
metadata chunks and these hashes. Original GitHub hero review remains independent.

## Actual product screenshot

`screenshot-monitor-1280x800.png` — 1280×800, 373805 bytes.
SHA256: `1C689BFBBB228286D409CE0ACE7CFD686F48CB0BED3C2B15D266B6A07B2C8D2B`

Captured from the real Debug SampleCaller with Chrome for Testing 153.0.8010.36.
Chrome loaded a fresh extraction of the exact CWS ZIP, SHA256
`A20C7781501F2DC43C8133D9C3F0EAD165E5E94BE6F4C0B3CACAACC3CED52A31`.
The test harness used an isolated temporary profile and its neutral local fixtures.
SampleCaller was moved onto the third physical monitor and resized through the
standard Windows UI to 1280×800. The capture is the actual window, with no crop,
resampling, compositing, text replacement or redaction. The capture API returned JPEG;
its decoded pixels were saved as PNG with no size/content edits. A pointer highlight at the
lower edge is from the capture interaction, not a product feature.

Observed: five Bound sessions; first Visible/ACTIVE without capture; remaining four
Parked with PARKED / LIVE and approximately 398 frames each. Four differently colored
neutral fixtures are visible. The small fixture text is actual captured webpage pixels.
The window was closed normally after capture and the isolated test harness exited.

Privacy review: no personal browser, account, profile path, filename, credential,
capability token, ChatGPT page or private document appears. Loopback fixture URLs,
ephemeral test session GUIDs and Chrome window IDs are intentional test identifiers;
they are not account identities or authentication capabilities. No unrelated desktop
content is included. No URL containing a token appears. Review is visual and scoped
to these exact pixels, not an automatic guarantee for later screenshots.

## Original brand assets

The window-and-bridge mark was created from original geometric rectangles and an
arch. It uses navy, white and teal; no Chrome/Google logo, third-party artwork or
external image was used. Transparent PNG icons are recognizable at native small
sizes. The 128px icon has 16px transparent margins. The small promo uses that same
mark and minimal original text; it depicts brand artwork, not a mock product UI.
It was visually checked at 440×280. All assets contain only approved PNG chunks.

| File | Dimensions | SHA256 |
| --- | --- | --- |
| icons/icon-16.png | 16×16 RGBA | `2EECD1B5C105752DD57362D17EF21EBB5A7C537D1B71AAE827567994936FDB17` |
| icons/icon-32.png | 32×32 RGBA | `1E010A124E60AF9090F93832D90498F90088C282F598C2CC9410F1850E04E1BB` |
| icons/icon-48.png | 48×48 RGBA | `677E57A26A2FC3A08F9E8251B2D9052C12CF0A485B078DBBA5406B94A33C30B6` |
| icons/icon-128.png | 128×128 RGBA | `38D1FC2E230AA76CE6DBBDB1123FA108F149DAC2D200331B3AC9D2D5D4233545` |
| promo-small-440x280.png | 440×280 RGB | `F2B34175479FE099B724B11386E20E659CAF28DB20CDAE499F1156A19C38B7AA` |

Icon paths are relative to `src/LazyChromeWindowBridge.Extension/`; the NuGet icon
is the identical 128px file. Brand assets contain no user data or hidden text metadata.
