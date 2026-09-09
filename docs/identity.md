# Product and governance identity

The product/display/repository name is **LazyChromeWindowBridge**.
The permanent Lazy AI Deck ProjectID is **LazyChromeExtension**.

The registered Source stays `C:\LazyAIDeckProjects\LazyChromeExtension`.
ProjectID and registered Source are immutable governance identity, not runtime product
names. Lazy AI Deck can consume the public Core API like another Windows application;
it is not a required runtime container.

Established Revision history retains its original semantic names and original names
of files/classes where those names describe the historical implementation. PLAN.md
and Revisions.md remain at the accepted baseline during candidate work.

Historical pre-release LazyChromeExtension geometry data is not a supported migration
contract. The renamed runtime uses `%LOCALAPPDATA%\LazyChromeWindowBridge\Geometry`;
there is no permanent compatibility alias or migration framework.

The explicit residual-name allowlist is maintained in legacy-name-allowlist.json.
The Source audit reports every remaining old-name content occurrence and any relative
file/directory path violation. Generated/ignored build output and transient handoff/
test evidence are not repository Source. Protected inherited governance is compared
byte-for-byte separately during executor acceptance.
