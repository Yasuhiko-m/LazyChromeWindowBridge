# Hero image privacy review

Image: [monitor-overview.png](monitor-overview.png), 1082×552 pixels.
SHA256: 59B5437CA2D7D7D2BED788A045D5FCAB571ECDE14A0D37A06D5EC3DCAED4E21A.

Captured from the actual unchanged SampleCaller on 2026-09-09, using the stable GUI
fixture with a newly isolated Chrome for Testing profile. Five neutral local color
fixtures were launched through the sample UI. One session remained ACTIVE; four
were PARKED/LIVE, with counters advancing independently and fresh images. The sample
was placed fully on the third physical monitor. Restore/re-PARK affected only the
selected session; Stop reached zero captures; normal sample/browser cleanup passed.

The image is a crop of the real window, keeping title, controls, all five session rows
and all five tiles. Only unused lower blank space was removed. No UI/text was fabricated,
redacted, composited or replaced. No personal browser/ChatGPT/account was used.

Visual inspection before retention and after cropping found no personal username,
machine name, filesystem path, token/capability, email, unrelated window/tab or account
data. Visible UUIDs/window IDs are ephemeral neutral fixture identities, not credentials;
visible URLs are local loopback fixtures only. No image-content interpretation exists
in product code. The release audit pins this reviewed image hash/dimensions, rejects
unexpected PNG text/EXIF chunks and requires manual re-review if it changes.

Raw captures and GUI logs remain ignored local evidence. This is the only public demo
image; it is not a gallery or proof of production-scale reliability.
