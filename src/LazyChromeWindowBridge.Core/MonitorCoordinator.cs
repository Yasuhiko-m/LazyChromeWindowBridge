using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LazyChromeWindowBridge.Core;

internal sealed record MonitorControl(bool Enabled, long Generation, CaptureOptions Options, bool Closing, NativeIdentity? Identity);
internal sealed class MonitorCoordinator(SessionRegistry sessions, GeometryCoordinator geometry)
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
    }
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private bool enabled, disposed;
    private long generation;
    private CaptureOptions options = new();
    private TaskCompletionSource changed = NewSignal();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Changed { get { lock (gate) return changed.Task; } }
    private void Signal() { var previous = changed; changed = NewSignal(); previous.TrySetResult(); }
    private void Invalidate(Entry entry, bool clearError = true, bool keepLatest = false)
    {
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
        var live = session?.State == SessionState.Bound && window is { Current: not null, State: not PlacementState.Closed } && window.Identity == entry.Identity;
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
        else if (entry.Connection is null) entry.State = "Disconnected";
        else entry.State = entry.Latest is null ? "Waiting" : "Live";
        return entry;
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
            enabled = true; options = requested;
            if (restarting) foreach (var entry in entries.Values) entry.Paused = false;
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
            if (sessions.Get(id)?.State != SessionState.Bound || window is not { Current: not null, State: PlacementState.Visible or PlacementState.Parked } ||
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
            if (disposed || sessions.Get(id)?.State != SessionState.Bound || geometry.Get(id) is not { Current: not null, State: not PlacementState.Closed }) return false;
            var entry = Sync(id);
            if (entry.Connection is not null) return false;
            entry.Connection = connection; Invalidate(entry, false); Signal();
            return true;
        }
    }
    public void Disconnect(Guid id, Guid connection)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Connection != connection) return;
            entry.Connection = null; entry.Capturing = false; Invalidate(entry, false); Sync(id); Signal();
        }
    }
    public MonitorControl Control(Guid id)
    {
        lock (gate)
        {
            var entry = Sync(id);
            return new(entry.Eligible && entry.Error is null, entry.Generation, options, disposed, entry.Identity);
        }
    }
    public void Status(Guid id, Guid connection, long frameGeneration, string? failure, bool capturing)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Connection != connection) return;
            // A stopped old generation confirms detach even after eligibility changed.
            if (!capturing) entry.Capturing = false;
            entry = Sync(id);
            if (entry.Generation != frameGeneration || !entry.Eligible || disposed) return;
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
        return !disposed && entry.Eligible && entry.Error is null && entry.Connection == connection &&
            entry.Generation == frameGeneration && identity is not null && entry.Identity == identity && sessions.Get(id)?.WindowId == windowId;
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
        var ratio = Math.Min(1, Math.Min((double)settings.MaxWidth / width, (double)settings.MaxHeight / height));
        if (ratio < 1)
        {
            width = Math.Max(1, (int)(width * ratio)); height = Math.Max(1, (int)(height * ratio));
            using var resized = new Bitmap(width, height);
            using (var graphics = Graphics.FromImage(resized)) { graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; graphics.DrawImage(image, 0, 0, width, height); }
            using var output = new MemoryStream();
            using var encoding = new EncoderParameters(1);
            encoding.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
            resized.Save(output, ImageCodecInfo.GetImageEncoders().Single(codec => codec.FormatID == ImageFormat.Jpeg.Guid), encoding);
            jpeg = output.ToArray();
        }
        lock (gate)
        {
            if (!Expected(id, connection, frameGeneration, windowId, identity)) return;
            var entry = entries[id];
            entry.Latest = new(id, entry.Identity!, windowId, tabId, entry.Generation, ++entry.Frames, width, height, jpeg, DateTimeOffset.UtcNow, captureMilliseconds);
            entry.Bytes += data.Length; entry.State = "Live"; entry.Capturing = true;
        }
    }
    public MonitorFrame? Latest(Guid id) { lock (gate) return Sync(id).Latest; }
    public MonitorSnapshot Snapshot()
    {
        lock (gate)
        {
            Reconcile();
            var rows = sessions.GetAll().Select(session =>
            {
                var entry = Sync(session.AppSessionId);
                return new SessionMonitorSnapshot(session.AppSessionId, session.WindowId, entry.Identity, entry.Generation, entry.State,
                    entry.Frames, entry.Bytes, entry.Latest?.ReceivedAt, entry.Latest?.Width ?? 0, entry.Latest?.Height ?? 0, entry.Error,
                    entry.Connection is not null, entry.Capturing);
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
        while (Snapshot().Connections != 0 && DateTimeOffset.UtcNow < until) await Task.Delay(50);
    }
}
