using System.Drawing;

namespace LazyChromeWindowBridge.Core;

internal sealed record MonitorControl(bool Enabled, long Generation, CaptureOptions Options, bool Closing, NativeIdentity? Identity, string? SourceSizeRequestId = null);
internal sealed class MonitorCoordinator(SessionRegistry sessions, GeometryCoordinator geometry, INativeCaptureFactory? captureFactory = null)
{
    private sealed class Entry
    {
        public NativeIdentity? Identity;
        public Guid? Connection;
        public long Generation, Frames, Bytes;
        public bool Eligible, Capturing, Paused;
        public MonitorFrame? Latest;
        public string? Error;
        public string State = "Unavailable";
        public CancellationTokenSource? NativeCancellation;
        public Task? NativeTask;
        public long NativeGeneration;
        public string? SourceSizeRequestId;
        public TaskCompletionSource<CaptureSourceSize>? SourceSize;
    }
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private bool enabled, disposed;
    private long generation;
    private CaptureOptions options = new();
    private readonly INativeCaptureFactory nativeCaptureFactory = captureFactory ?? new NativeWindowCaptureFactory();
    private readonly HashSet<Task> nativeWorkers = [];
    private TaskCompletionSource changed = NewSignal();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Changed { get { lock (gate) return changed.Task; } }
    private void Signal() { var previous = changed; changed = NewSignal(); previous.TrySetResult(); }
    private void Invalidate(Entry entry, bool clearError = true, bool keepLatest = false)
    {
        entry.NativeCancellation?.Cancel();
        entry.Generation = ++generation;
        if (!keepLatest) entry.Latest = null;
        if (clearError) entry.Error = null;
    }
    private Entry Sync(Guid id)
    {
        if (!entries.TryGetValue(id, out var entry)) entries.Add(id, entry = new());
        var session = sessions.Get(id);
        var window = geometry.Get(id);
        entry.Identity ??= window?.Identity;
        // Current is a best-effort geometry observation. Exact mapping, placement and
        // NativeIdentity are the monitor ownership boundary; a transient Read/DPI failure
        // must not turn a live owned window into a monitor lifecycle transition.
        var live = session?.State == SessionState.Bound && window is { State: not PlacementState.Closed } && window.Identity == entry.Identity;
        var eligible = enabled && !disposed && !entry.Paused && live && window!.State is PlacementState.Visible or PlacementState.Parked;
        // Placement is not monitor identity. A successful PARK/RESTORE keeps the
        // same native owner, transport generation and latest JPEG.
        if (entry.Eligible != eligible)
        {
            entry.Eligible = eligible;
            Invalidate(entry, keepLatest: enabled && !disposed && live && entry.Paused); Signal();
        }
        if (!live) { entry.Latest = null; entry.State = "Unavailable"; }
        else if (!enabled || disposed) { entry.Latest = null; entry.State = "Stopped"; }
        else if (entry.Paused) entry.State = "Paused";
        else if (!eligible) { entry.Latest = null; entry.State = window!.State.ToString(); }
        else if (entry.Error is not null) entry.State = "Error";
        else if (options.Mode == CaptureMode.BrowserViewport && entry.Connection is null) entry.State = "Disconnected";
        else entry.State = entry.Latest is null ? "Waiting" : "Live";
        EnsureNativeWorker(id, entry, window);
        return entry;
    }

    private void EnsureNativeWorker(Guid id, Entry entry, WindowSnapshot? window)
    {
        var shouldRun = entry.Eligible && entry.Error is null && options.Mode == CaptureMode.NativeWindow &&
            window is { WindowId: not null } && window.Identity == entry.Identity;
        if (!shouldRun)
        {
            entry.NativeCancellation?.Cancel();
            return;
        }
        if (entry.NativeTask is { IsCompleted: false } && entry.NativeGeneration == entry.Generation) return;
        entry.NativeCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        var workerGeneration = entry.Generation;
        var identity = entry.Identity!;
        var windowId = window!.WindowId!.Value;
        entry.NativeCancellation = cancellation;
        entry.NativeGeneration = workerGeneration;
        var worker = Task.Run(() => NativeLoop(id, workerGeneration, windowId, identity, cancellation.Token));
        entry.NativeTask = worker;
        nativeWorkers.Add(worker);
        _ = worker.ContinueWith(completed => { lock (gate) nativeWorkers.Remove(completed); }, TaskScheduler.Default);
    }

    private async Task NativeLoop(Guid id, long workerGeneration, int windowId, NativeIdentity identity, CancellationToken token)
    {
        INativeCaptureSession? capture = null;
        try
        {
            if (geometry.RequireOwned(id) != identity) throw new InvalidOperationException("Native capture identity became stale before acquisition.");
            capture = nativeCaptureFactory.Open(identity);
            lock (gate)
            {
                if (!ExpectedNative(id, workerGeneration, windowId, identity)) return;
                entries[id].Capturing = true;
            }
            while (!token.IsCancellationRequested)
            {
                CaptureOptions requested;
                lock (gate)
                {
                    if (!ExpectedNative(id, workerGeneration, windowId, identity)) return;
                    requested = options;
                }
                if (geometry.RequireOwned(id) != identity) throw new InvalidOperationException("Native capture identity became stale before frame acquisition.");
                var started = DateTimeOffset.UtcNow;
                var frame = await capture.CaptureAsync(requested, token).ConfigureAwait(false);
                if (geometry.RequireOwned(id) != identity) throw new InvalidOperationException("Native capture identity became stale during frame acquisition.");
                AcceptNative(id, workerGeneration, windowId, identity, requested, frame);
                var interval = TimeSpan.FromSeconds(1d / requested.FramesPerSecond);
                var remaining = interval - (DateTimeOffset.UtcNow - started);
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            lock (gate)
            {
                if (ExpectedNative(id, workerGeneration, windowId, identity))
                {
                    var entry = entries[id];
                    entry.Error = error.Message[..Math.Min(240, error.Message.Length)];
                    entry.Latest = null;
                    entry.State = "Error";
                    entry.Capturing = false;
                    Signal();
                }
            }
        }
        finally
        {
            if (capture is not null) await capture.DisposeAsync().ConfigureAwait(false);
            lock (gate)
            {
                if (entries.TryGetValue(id, out var entry) && entry.NativeGeneration == workerGeneration)
                {
                    entry.Capturing = false;
                    entry.NativeTask = null;
                    entry.NativeCancellation?.Dispose();
                    entry.NativeCancellation = null;
                }
            }
        }
    }
    public void Reconcile()
    {
        lock (gate)
        {
            foreach (var session in sessions.GetAll()) Sync(session.AppSessionId);
            foreach (var id in entries.Keys.Where(id => sessions.Get(id) is null && entries[id].Connection is null).ToArray()) entries.Remove(id);
        }
    }
    public void Start(CaptureOptions requested)
    {
        requested.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var restarting = !enabled;
            var modeChanged = enabled && options.Mode != requested.Mode;
            var regionChanged = enabled && options.Region != requested.Region;
            enabled = true; options = requested;
            if (restarting) foreach (var entry in entries.Values) entry.Paused = false;
            if (modeChanged || regionChanged) foreach (var entry in entries.Values) Invalidate(entry, keepLatest: entry.Paused);
            foreach (var session in sessions.GetAll())
            {
                var entry = Sync(session.AppSessionId);
                // Ordinary option updates only signal control. Explicit Start
                // after a real failure remains the separate retry boundary.
                if (!entry.Paused && entry.Error is not null) { Invalidate(entry); Sync(session.AppSessionId); }
            }
            Signal();
        }
    }
    public void SetSessionMonitoring(Guid id, bool requested)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!enabled) throw new InvalidOperationException("Start global monitoring before changing a session.");
            var entry = Sync(id);
            var window = geometry.Get(id);
            if (sessions.Get(id)?.State != SessionState.Bound || window is not { State: PlacementState.Visible or PlacementState.Parked } ||
                window.Identity != entry.Identity)
                throw new InvalidOperationException("Session monitoring requires a live owned native window.");
            if (entry.Paused == !requested)
            {
                if (!requested || entry.Error is null) return;
                // Explicit ON after a real error retries only this session.
                Invalidate(entry);
            }
            entry.Paused = !requested;
            Sync(id); Signal();
        }
    }
    public void Stop()
    {
        lock (gate)
        {
            if (!enabled) return;
            enabled = false;
            foreach (var id in entries.Keys.ToArray()) Sync(id);
            Signal();
        }
    }
    public bool Connect(Guid id, Guid connection)
    {
        lock (gate)
        {
            if (disposed || sessions.Get(id)?.State != SessionState.Bound || geometry.Get(id) is not { State: not PlacementState.Closed }) return false;
            var entry = Sync(id);
            if (entry.Connection is not null) return false;
            entry.Connection = connection;
            if (options.Mode == CaptureMode.BrowserViewport) Invalidate(entry, false);
            Signal();
            return true;
        }
    }
    public void Disconnect(Guid id, Guid connection)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Connection != connection) return;
            entry.Connection = null;
            if (options.Mode == CaptureMode.BrowserViewport) { entry.Capturing = false; Invalidate(entry, false); }
            Sync(id); Signal();
        }
    }
    public MonitorControl Control(Guid id)
    {
        lock (gate)
        {
            var entry = Sync(id);
            return new(entry.Eligible && entry.Error is null && options.Mode == CaptureMode.BrowserViewport, entry.Generation, options, disposed, entry.Identity, entry.SourceSizeRequestId);
        }
    }
    public void Status(Guid id, Guid connection, long frameGeneration, string? failure, bool capturing)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Connection != connection) return;
            // A stopped old generation confirms detach even after eligibility changed.
            if (!capturing && options.Mode == CaptureMode.BrowserViewport) entry.Capturing = false;
            entry = Sync(id);
            if (entry.Generation != frameGeneration || !entry.Eligible || disposed || options.Mode != CaptureMode.BrowserViewport) return;
            entry.Capturing = capturing;
            if (failure is not null)
            {
                entry.Error = failure[..Math.Min(240, failure.Length)]; entry.Latest = null; entry.State = "Error"; Signal();
            }
        }
    }
    private bool Expected(Guid id, Guid connection, long frameGeneration, int windowId, NativeIdentity? identity)
    {
        var entry = Sync(id);
        return !disposed && options.Mode == CaptureMode.BrowserViewport && entry.Eligible && entry.Error is null && entry.Connection == connection &&
            entry.Generation == frameGeneration && identity is not null && entry.Identity == identity && sessions.Get(id)?.WindowId == windowId;
    }
    private bool ExpectedNative(Guid id, long frameGeneration, int windowId, NativeIdentity identity)
    {
        var entry = Sync(id);
        return !disposed && options.Mode == CaptureMode.NativeWindow && entry.Eligible && entry.Error is null &&
            entry.Generation == frameGeneration && entry.Identity == identity && sessions.Get(id)?.WindowId == windowId;
    }
    public void Accept(Guid id, Guid connection, long frameGeneration, int windowId, int tabId, NativeIdentity? identity, string data, double captureMilliseconds)
    {
        CaptureOptions settings;
        lock (gate)
        {
            if (!Expected(id, connection, frameGeneration, windowId, identity)) return;
            settings = options;
        }
        if (data.Length > 2800000 || tabId < 0 || !double.IsFinite(captureMilliseconds)) throw new ArgumentException("Invalid monitor frame metadata.");
        var jpeg = Convert.FromBase64String(data);
        if (jpeg.Length < 4 || jpeg[0] != 0xff || jpeg[1] != 0xd8) throw new ArgumentException("Expected a JPEG monitor frame.");
        using var input = new MemoryStream(jpeg);
        using var image = Image.FromStream(input, false, true);
        if (image.Width > 8192 || image.Height > 8192 || (long)image.Width * image.Height > 24000000) throw new ArgumentException("Monitor image exceeds bounds.");
        var width = image.Width; var height = image.Height;
        if (settings.Resize is null && (width > settings.MaxWidth || height > settings.MaxHeight)) throw new ArgumentException("BrowserViewport monitor frame exceeds requested bounds.");
        lock (gate)
        {
            if (!Expected(id, connection, frameGeneration, windowId, identity)) return;
            var entry = entries[id];
            entry.Latest = new(id, entry.Identity!, windowId, tabId, entry.Generation, ++entry.Frames, width, height, jpeg, DateTimeOffset.UtcNow, captureMilliseconds);
            entry.Bytes += data.Length; entry.State = "Live"; entry.Capturing = true;
        }
    }
    private void AcceptNative(Guid id, long frameGeneration, int windowId, NativeIdentity identity, CaptureOptions requested, NativeCaptureFrame frame)
    {
        if (frame.Jpeg.Length < 4 || frame.Jpeg[0] != 0xff || frame.Jpeg[1] != 0xd8 || frame.Width < 1 || frame.Height < 1 ||
            !double.IsFinite(frame.CaptureMilliseconds)) throw new ArgumentException("Invalid NativeWindow monitor frame.");
        lock (gate)
        {
            if (!ExpectedNative(id, frameGeneration, windowId, identity)) return;
        }
        if (requested.Resize is null && (frame.Width > requested.MaxWidth || frame.Height > requested.MaxHeight)) throw new ArgumentException("NativeWindow monitor frame exceeds requested bounds.");
        using (var input = new MemoryStream(frame.Jpeg))
        using (var image = Image.FromStream(input, false, true))
            if (image.Width != frame.Width || image.Height != frame.Height) throw new ArgumentException("NativeWindow JPEG dimensions do not match metadata.");
        lock (gate)
        {
            // A same-mode update deliberately keeps the generation. An acquisition
            // already in flight under the prior snapshot is obsolete, not an error;
            // the next loop iteration performs the GPU resize with current bounds.
            if (!ExpectedNative(id, frameGeneration, windowId, identity) || options != requested) return;
            var entry = entries[id];
            entry.Latest = new(id, identity, windowId, -1, entry.Generation, ++entry.Frames, frame.Width, frame.Height,
                frame.Jpeg, DateTimeOffset.UtcNow, frame.CaptureMilliseconds) { Mode = CaptureMode.NativeWindow };
            entry.Bytes += frame.Jpeg.Length;
            entry.State = "Live";
            entry.Capturing = true;
        }
    }
    // Public latest-frame polling is observational. Reconciliation happens only on the
    // established lifecycle boundaries (geometry change/poll, start/stop, transport and
    // session controls), never merely because a caller repaints a preview.
    public MonitorFrame? Latest(Guid id) { lock (gate) return entries.TryGetValue(id, out var entry) ? entry.Latest : null; }
    public async ValueTask<CaptureSourceSize> GetCaptureSourceSizeAsync(Guid id, CaptureMode mode, CancellationToken token = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Unknown capture mode.");
        if (mode == CaptureMode.NativeWindow)
        {
            NativeIdentity identity;
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                var entry = Sync(id); var window = geometry.Get(id);
                if (sessions.Get(id)?.State != SessionState.Bound || window is not { State: PlacementState.Visible or PlacementState.Parked } || window.Identity != entry.Identity)
                    throw new InvalidOperationException("Capture source size requires a live owned native window.");
                identity = entry.Identity!;
            }
            if (geometry.RequireOwned(id) != identity) throw new InvalidOperationException("Native capture identity became stale before size acquisition.");
            await using var capture = nativeCaptureFactory.Open(identity);
            var size = await capture.GetSourceSizeAsync(token).ConfigureAwait(false);
            if (geometry.RequireOwned(id) != identity) throw new InvalidOperationException("Native capture identity became stale during size acquisition.");
            return size;
        }
        Task<CaptureSourceSize> wait;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var entry = Sync(id);
            if (sessions.Get(id)?.State != SessionState.Bound || entry.Connection is null || entry.Identity is null)
                throw new InvalidOperationException("BrowserViewport source size requires the owned extension control connection.");
            if (entry.SourceSize is not null) throw new InvalidOperationException("A source-size query is already active for this session.");
            entry.SourceSizeRequestId = Guid.NewGuid().ToString("N");
            entry.SourceSize = new(TaskCreationOptions.RunContinuationsAsynchronously);
            wait = entry.SourceSize.Task; Signal();
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { return await wait.WaitAsync(timeout.Token).ConfigureAwait(false); }
        finally { lock (gate) { if (entries.TryGetValue(id, out var entry) && entry.SourceSize?.Task == wait) { entry.SourceSize = null; entry.SourceSizeRequestId = null; Signal(); } } }
    }
    public void SourceSize(Guid id, Guid connection, string requestId, double width, double height)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Connection != connection || entry.SourceSizeRequestId != requestId ||
                !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return;
            entry.SourceSize?.TrySetResult(new(width, height));
        }
    }
    public void SourceSizeFailure(Guid id, Guid connection, string requestId, string? error)
    {
        lock (gate)
            if (entries.TryGetValue(id, out var entry) && entry.Connection == connection && entry.SourceSizeRequestId == requestId)
                entry.SourceSize?.TrySetException(new InvalidOperationException(error ?? "BrowserViewport source-size query failed."));
    }
    public MonitorSnapshot Snapshot()
    {
        lock (gate)
        {
            var rows = sessions.GetAll().Select(session =>
            {
                if (!entries.TryGetValue(session.AppSessionId, out var entry))
                    return new SessionMonitorSnapshot(session.AppSessionId, session.WindowId, null, 0, "Unavailable", 0, 0, null, 0, 0, null, false, false)
                        { Mode = options.Mode };
                return new SessionMonitorSnapshot(session.AppSessionId, session.WindowId, entry.Identity, entry.Generation, entry.State,
                    entry.Frames, entry.Bytes, entry.Latest?.ReceivedAt, entry.Latest?.Width ?? 0, entry.Latest?.Height ?? 0, entry.Error,
                    entry.Connection is not null, entry.Capturing)
                    { Mode = options.Mode };
            }).ToArray();
            return new(enabled, rows.Sum(r => r.Frames), rows.Sum(r => r.Bytes), entries.Values.Count(e => e.Connection is not null),
                entries.Values.Count(e => e.Capturing), rows);
        }
    }
    public async Task ShutdownAsync()
    {
        Stop();
        lock (gate) { disposed = true; Signal(); }
        var until = DateTimeOffset.UtcNow.AddSeconds(7); // Includes bounded five-second in-flight acquisition.
        while ((Snapshot().Connections != 0 || ActiveNativeWorkers()) && DateTimeOffset.UtcNow < until) await Task.Delay(50);
    }
    private bool ActiveNativeWorkers() { lock (gate) return nativeWorkers.Count != 0; }
}
