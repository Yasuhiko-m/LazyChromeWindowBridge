namespace LazyChromeWindowBridge.Core;

internal sealed record DownloadReport(Guid BridgeId, Guid BrowserSessionId, long Sequence, DownloadLifecycleEvent Event);

// One configured Chrome profile per runtime. A high-water mark outlives snapshot eviction.
internal sealed class DownloadTracker(Action<DownloadLifecycleEvent> publish) : IDisposable
{
    internal const int Capacity = 256;
    private readonly object gate = new();
    private readonly Dictionary<int, DownloadLifecycleEvent> latest = [];
    private readonly Queue<int> order = new();
    private Guid? browserSessionId;
    private long lastSequence;
    private bool closed;

    internal static bool Valid(DownloadReport? report) => report is { Event: { } value } &&
        report.BridgeId != Guid.Empty && report.BrowserSessionId != Guid.Empty &&
        report.Sequence is > 0 and <= 9007199254740991 && value.DownloadId >= 0 &&
        Enum.IsDefined(value.State) && value.Filename is { Length: <= 1024 } &&
        !value.Filename.Contains('\0') && value.Error is null or { Length: <= 128 } &&
        (value.State == DownloadLifecycleState.Interrupted || value.Error is null) &&
        value.ObservedAt != default;

    internal bool Receive(DownloadReport report)
    {
        lock (gate)
        {
            if (closed || (browserSessionId is not null && browserSessionId != report.BrowserSessionId)) return false;
            browserSessionId = report.BrowserSessionId;
            if (report.Sequence <= lastSequence) return true;
            lastSequence = report.Sequence;
            var value = report.Event;
            if (latest.TryGetValue(value.DownloadId, out var previous))
            {
                if (previous.State == DownloadLifecycleState.Complete || previous.State == value.State ||
                    value.State == DownloadLifecycleState.Created) return true;
            }
            else
            {
                if (latest.Count == Capacity) latest.Remove(order.Dequeue());
                order.Enqueue(value.DownloadId);
            }
            latest[value.DownloadId] = value;
            // Serialized synchronous callbacks preserve order, including concurrent HTTP retries.
            // BridgeRuntime isolates each subscriber exception. Subscribers must return promptly.
            publish(value);
            return true;
        }
    }
    internal DownloadLifecycleEvent[] GetAll() { lock (gate) return order.Select(id => latest[id]).ToArray(); }
    internal DownloadLifecycleEvent? Get(int id) { lock (gate) return latest.GetValueOrDefault(id); }
    public void Dispose() { lock (gate) { closed = true; latest.Clear(); order.Clear(); } }
}
