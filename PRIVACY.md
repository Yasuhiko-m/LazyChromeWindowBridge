# LazyChromeWindowBridge privacy policy

Applies to version 0.1.0. LazyChromeWindowBridge connects an
extension to a Windows companion on the same computer. It handles user data locally;
it does not mean that no user data is handled.

## What the bridge handles and why

- Original launch URLs identify the windows the companion launches and their saved
  placement. The companion persists the original URL, normal window bounds, DPI and
  save time in local geometry files. Subsequent navigation does not redefine ownership.
- Session, Chrome tab/window/browser, process and native window identifiers establish
  exact ownership and detect closed or replaced windows. Local capability tokens
  authenticate the companion connection; these are not website passwords or login tokens.
- When the user starts monitoring, the extension captures temporary JPEG thumbnails
  of owned PARKED windows. The companion displays these pixels to that user. Visible
  windows are ACTIVE and are not captured. Frames are replaced in memory; the product
  does not keep a screenshot archive, perform OCR or analyze webpage meaning.
- Chrome Download Manager events provide download IDs, lifecycle state, absolute
  filenames, error state and observation times. This observation is profile-global,
  not attributed to a particular launched window. Incognito items are excluded.
  The bridge does not read downloaded files or collect download URLs/content.
  Absolute filenames can reveal personal names and private folder information.
- Bounded `chrome.storage.session` state supports service-worker recovery and delivery:
  ownership records, local capabilities, capture target IDs and a download outbox.
  Ownership records are limited to 256. Download state is limited to 128 tracked items,
  256 pending events and 16 companion
  representatives. This is transient browser-session state, not a persistent download
  history database. It is restricted to trusted extension contexts.

Thumbnails may contain any sensitive information visible on the monitored page,
including messages, health or financial information. Choose which pages to launch
and monitor accordingly. The bridge does not extract DOM text, inspect network
responses, use OCR, automate page input or determine page completion.

## Location, retention and control

Bridge messages stay on the user's machine and use capability-authenticated HTTP
loopback at `127.0.0.1`. Local HTTP is not TLS encryption. No publisher server or cloud
relay receives bridge data. Websites opened by the user still make their own normal
network requests under their own privacy policies.

Stop monitor to stop captures; restore a window to stop that window's PARKED capture.
Closing the companion ends its runtime and restores parked windows. Transient runtime
data ends with that runtime; extension session state can survive worker suspension
but not a browser-session restart. Closing/retiring bindings removes their records.
Saved geometry persists across runs until the user removes the companion's geometry
files (the default is the local application-data LazyChromeWindowBridge geometry
directory, or the directory chosen by the integrating application).

The supplied companion does not log absolute download filenames in production.
Integrating applications receive the public API data and may implement different
storage or logging; their own disclosures apply. The publisher cannot remotely
retrieve or erase local files.

## Sharing and limited use

There is no telemetry, advertising, cloud relay, data sale, behavioral profiling or
developer access to your bridge data. Data is passed only to the authenticated local
companion to provide the features above. LazyChromeWindowBridge's use of information
received through Chrome APIs adheres to the Chrome Web Store User Data Policy,
including its Limited Use requirements. It is not used for advertising, creditworthiness
or unrelated purposes, and is not transferred to data brokers or read by publisher staff.

## Contact

For privacy questions use the project's
[support page](https://github.com/Yasuhiko-m/LazyChromeWindowBridge/issues).
Do not post private filenames, tokens, screenshots or other sensitive content in
public issues. Changes to this policy will be recorded in this public document.
