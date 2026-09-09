# Download lifecycle

`BridgeRuntime` observes Chrome Download Manager state, independently of page content.
The [official Chrome downloads API](https://developer.chrome.com/docs/extensions/reference/api/downloads)
supplies events and exact `DownloadItem.filename`; terminal changes are completed with
`search({id})`, because change deltas need not contain the full filename or error.
Only the `downloads` permission is added. Its broad capability is intentionally used
for read-only onCreated/onChanged/search, never initiation or mutation.

## Public API and consumer responsibility
```csharp
public enum DownloadLifecycleState { Created, Complete, Interrupted }
public sealed record DownloadLifecycleEvent(
    int DownloadId, DownloadLifecycleState State, string Filename,
    string? Error, DateTimeOffset ObservedAt);

// BridgeRuntime members:
public event EventHandler<DownloadLifecycleEvent>? DownloadChanged;
public DownloadLifecycleEvent[] GetDownloads();
public DownloadLifecycleEvent? GetDownload(int downloadId);
```

Subscribe before launching. Notifications run synchronously on a transport thread;
return promptly and queue UI/filesystem work in your application. Each synchronous
subscriber exception is isolated so later subscribers and acknowledgements proceed.
Handle asynchronous consumer failures yourself; avoid async-void handlers and blocking
waits (including waiting for shutdown inside a callback). Delivery is ordered per ID.
Snapshots are immutable values in a copied array, at most 256 latest IDs in first-seen
order; they are not Chrome history. Disposal clears them and subscriptions.

Created reports the onCreated filename, which can be empty. Complete/Interrupted use
the current official full-item filename and an interrupt reason when available.
ObservedAt is the extension's observation/recovery time, not Chrome's original event
timestamp or an estimate of webpage completion. Complete is final in this stream;
unrelated exists/filename changes cannot turn it into Interrupted. Resume/retry history
is not modeled; repeated Interrupted states are coalesced and a later Complete can follow.

Chrome `state=complete` is browser completion authority. The consumer must then check
its exact expected directory and filename grammar, existence, stable size/LastWriteTime
and exclusive access before moving the file. Those checks do not eliminate races after
the handle closes. The bridge never opens, scans, validates or moves downloaded files.
Filename may exist before Complete; it is not a readiness signal.

## Profile scope and authentication
DownloadItem has no source tab/window identity. There is no appSessionId attribution
and no URL/finalUrl/referrer in the public payload or download outbox. Observation covers
the connected non-incognito Chrome profile, including its unrelated windows. Use a
dedicated profile if the consumer should not observe unrelated download metadata.

Bindings group by exact bridgeId/origin. One eligible live binding authenticates the
existing listener's POST `/lazy-chrome-window-bridge/api/downloads` using a bearer
capability and X-Bridge-Session header. That session is not the source of the download.
Closing it selects another representative at the next reconciliation. Five sessions
in one Bridge receive one stream. Independent Bridges in that profile may each receive
once. A runtime accepts the first authenticated browserSessionId for its lifetime;
multiple profiles and caller takeover are outside this contract.

## Delivery and exact recovery boundary
Before transport, the extension persists transitions and their destination Bridges in
`chrome.storage.session`. Terminal drafts are persisted even if the full-item lookup
fails; the existing worker/alarm path retries lookup and delivery. Known IDs are searched
on recovery to find missed terminal changes. No arbitrary historical download search
or import occurs. Downloads created before an eligible binding exists are not imported;
newly connected Bridges do not receive older streams intended for other Bridges.

Each transition has a monotonic browser-session sequence. Delivery is serial within a
Bridge, with independent fan-out across Bridges. Core updates a high-water mark and
latest snapshot before invoking subscribers. A lost HTTP acknowledgement retries the
same sequence, without another notification, even if that snapshot has been evicted.
Only successful HTTP acknowledgements remove destinations from the durable outbox.

This covers ordinary MV3 worker restart and temporary loopback failure within the same
running browser session and live BridgeRuntime, provided retention capacity is not
exceeded. It does not cover a crash before the first storage write, extension disable/
reload/update, cleared storage, full browser restart, caller restart/takeover, or
downloads removed from Chrome history before terminal metadata can be resolved.
No disk log, database, cloud or permanent download history is added.

| Retained/work bound | Limit and behavior |
| --- | --- |
| Extension observed IDs | 128; terminal streams are retired oldest-first, then oldest active |
| Pending transitions | 256; overflow retires whole streams and their pending events, so notification loss is possible at capacity |
| Bridge destinations | 16 per stream; deterministic live binding order chooses the supported set |
| Filename / error | 1024 / 128 characters; invalid/oversize data is not delivered or truncated |
| Core snapshots | 256 latest IDs; eviction does not reset retry deduplication |
| HTTP body / request | 16 KiB / 3 seconds; existing capability-authenticated listener |
| Delivery work | At most 16 reports per Bridge per flush; retry on existing reconciliation |
| Sequence | JavaScript safe positive integer; exhaustion stops append rather than wrapping |

Unavailable destinations remain pending within these limits. Observation is best effort
at overflow and outside the stated lifetime. A failed/unresolved event blocks later
delivery to its destination to preserve ordering; other Bridges can continue.

## Privacy and tests
Absolute filenames may reveal sensitive local paths. Production never logs them and
never collects download URLs, network bodies, DOM, OCR, semantic content or webpage
completion signals. The downloads permission does not grant the product a new input
or file-mutation feature. Metadata remains local in bounded session storage and memory.

`Scripts/Test-All.ps1` includes deterministic retry/ordering/capacity/exception tests.
With Chrome for Testing, it also runs real neutral ZIP, deliberate interruption,
five-session deduplication, worker restart and post-Complete consumer move acceptance.
The ignored logs contain only neutral fixture paths and metadata.
