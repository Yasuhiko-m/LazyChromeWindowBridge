# Security and privacy

## Local authentication
The bridge listens only on 127.0.0.1 at an ephemeral port and rejects other Host names.
It grants no website CORS access. Each launch has a random 256-bit capability, sent
in the bootstrap fragment rather than the HTTP path/query. Binding/native/close
requests require the session's bearer capability. WebSocket monitoring authenticates
before accepting frames and retains exact per-session connection identity.

Bootstrap responses disable caching/referrers. HTTP bodies and WebSocket
authentication/frame messages have bounds. Extension requests refuse redirects and
time out. Runtime logging providers are disabled so capabilities and user URLs are
not logged. Test summaries omit capabilities and use isolated neutral pages.

This protects against unauthenticated websites; it is not a security boundary against
malicious local processes running as the same Windows user. Such processes may observe
browser state, arguments or process memory. Do not treat loopback alone as authentication.

## Permissions and ownership
The extension requests only storage, alarms, debugger, downloads and 127.0.0.1 host permission.
Its content script runs only on the exact local bootstrap path, never on a remote
application page. It reads the bootstrap location, not document content.

Chrome's debugger permission itself is broad. BrowserViewport product code uses only
Page.getLayoutMetrics and Page.captureScreenshot for human thumbnails. It does not
use Runtime/DOM/Network inspection or browser input commands. Monitoring can attach
the debugger to live owned Visible or Parked windows. Placement/option changes retain
that same-tab attachment; Stop detaches capture and retains idle control sockets.
LCWB-owned launches use --silent-debugger-extension-api for best-effort Chrome infobar
suppression. This does not narrow or weaken debugger permission and is not a safety
guarantee; the extension itself does not remove notices. Chrome may ignore the flag
or reuse a process without it. Capture does not require suppression. When a notice exists,
user cancellation blocks the affected request until an explicit new generation.
Other debuggers can contend for the BrowserViewport target. NativeWindow capture does
not attach the debugger; it uses the already-authoritative HWND directly through WGC,
with identity checks before and across acquisition and no picker or window enumeration.
Caller policy controls session pause/resume; PARK/RESTORE never auto-toggles monitoring.
Pause detaches only its target and retains the last JPEG as Paused/frozen; global Stop
clears it. Frozen data is still sensitive imagery, with no semantic processing.
NativeWindow may also include Chrome non-client chrome, title bar and border.

Chrome's downloads permission is also broad. Product calls are only onCreated/onChanged
listeners and search({id}); no initiation, cancellation, pause/resume, removal, opening,
filename override or danger acceptance. Download transport uses one live session's
bearer capability and X-Bridge-Session header for authentication, never attribution.
Bridge/profile identifiers must match that bound session. The existing listener uses
a 16 KiB limit for this route; no new listener, host or website CORS grant is added.

Session capability, browser/window IDs and retained HWND/PID/property protect against
cross-binding and reused native handles. Late or wrong-generation frames are rejected.
A capture failure cannot change session ownership or saved Normal geometry.

Taskbar control uses that same exact native identity. It changes only APPWINDOW/
TOOLWINDOW classification on the selected HWND, never enumerates peer Chrome windows,
does not activate the target and restores the captured original style on normal release.
The policy is memory-only and is not added to geometry profiles.

## Data boundaries
JPEGs contain monitored pixels and can therefore contain sensitive content.
BrowserViewport frames use local loopback transport; NativeWindow surfaces and frames
stay inside Core. Both modes keep only the bounded latest encoded frame in local memory.
The product keeps only the latest frame; it has no recording archive, telemetry,
cloud relay, OCR, semantic analysis, webpage completion detection or output extraction.

Downloads expose browser metadata only: ID, lifecycle, exact absolute filename,
optional interrupt reason and observation time. Filename can reveal sensitive local
paths. Production never logs it or reads the file. There is no download URL, referrer,
response body or page-content collection. The bounded outbox lives in storage.session;
Core snapshots are in memory. There is no download database or permanent product log.
Observation includes downloads from unrelated windows of the same non-incognito
profile while a Bridge is connected; use a dedicated profile where isolation matters.

Geometry profiles store the normalized original launch URL, Normal rectangle, DPI
and timestamp. URLs themselves can be sensitive; choose launch URLs accordingly and
protect the user-data/geometry directories using normal Windows account protections.
Do not put credentials in launch URLs; embedded URL credentials are rejected.

Use isolated browser profiles and data directories for tests. Never commit test
profiles, logs, screenshots, tokens, account data or generated output. The repository
does not include authentication material or a secret-management subsystem.
