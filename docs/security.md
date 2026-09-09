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
The extension requests only storage, alarms, debugger and 127.0.0.1 host permission.
Its content script runs only on the exact local bootstrap path, never on a remote
application page. It reads the bootstrap location, not document content.

Chrome's debugger permission itself is broad. Product code uses only
Page.getLayoutMetrics and Page.captureScreenshot for human thumbnails. It does not
use Runtime/DOM/Network inspection or browser input commands. Visible windows have
no monitoring debugger attachment. Normal Chrome debugging notices remain visible;
user cancellation blocks the affected request until an explicit new generation.
Other debuggers can contend for the target.

Session capability, browser/window IDs and retained HWND/PID/property protect against
cross-binding and reused native handles. Late or wrong-generation frames are rejected.
A capture failure cannot change session ownership or saved Normal geometry.

## Data boundaries
JPEGs contain the monitored page's visible pixels and can therefore contain sensitive
content. They remain in local memory and loopback transport for human display.
The product keeps only the latest frame; it has no recording archive, telemetry,
cloud relay, OCR, semantic analysis, completion detection or output extraction.

Geometry profiles store the normalized original launch URL, Normal rectangle, DPI
and timestamp. URLs themselves can be sensitive; choose launch URLs accordingly and
protect the user-data/geometry directories using normal Windows account protections.
Do not put credentials in launch URLs; embedded URL credentials are rejected.

Use isolated browser profiles and data directories for tests. Never commit test
profiles, logs, screenshots, tokens, account data or generated output. The repository
does not include authentication material or a secret-management subsystem.
