using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CallerHarness;

internal sealed record CaptureOptions(int FramesPerSecond = 2, int MaxWidth = 240, int MaxHeight = 135)
{
    public void Validate()
    {
        if (FramesPerSecond is < 1 or > 10 || MaxWidth is < 160 or > 1920 || MaxHeight is < 90 or > 1080)
            throw new ArgumentException("Monitor supports 1–10 fps and output up to 1920×1080.");
    }
}
internal sealed record MonitorControl(bool Enabled, long Generation, CaptureOptions Options, bool Closing, NativeIdentity? Identity);
internal sealed record MonitorFrame(Guid AppSessionId, NativeIdentity Identity, int WindowId, int TabId,
    long Generation, long Sequence, int Width, int Height, byte[] Jpeg, DateTimeOffset ReceivedAt, double CaptureMilliseconds);
internal sealed record SessionMonitorSnapshot(Guid AppSessionId, int? WindowId, NativeIdentity? Identity, long Generation,
    string State, long Frames, long Bytes, DateTimeOffset? LastFrameAt, int Width, int Height, string? Error, bool Connected, bool Capturing);
internal sealed record MonitorSnapshot(bool Enabled, long Frames, long Bytes, int Connections, int CapturingConnections, SessionMonitorSnapshot[] Sessions);

internal sealed class MonitorCoordinator(SessionRegistry sessions, GeometryCoordinator geometry)
{
    private sealed class Entry
    {
        public NativeIdentity? Identity;
        public Guid? Connection;
        public long Generation, PlacementGeneration = -1, Frames, Bytes;
        public PlacementState? Placement;
        public bool Eligible, Capturing;
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
    private void Invalidate(Entry entry, bool clearError = true)
    {
        entry.Generation = ++generation; entry.Latest = null;
        if (clearError) entry.Error = null;
    }
    private Entry Sync(Guid id)
    {
        if (!entries.TryGetValue(id, out var entry)) entries.Add(id, entry = new());
        var session = sessions.Get(id);
        var window = geometry.Get(id);
        entry.Identity ??= window?.Identity;
        var live = session?.State == SessionState.Bound && window is { Current: not null, State: not PlacementState.Closed } && window.Identity == entry.Identity;
        var eligible = enabled && !disposed && live && window!.State == PlacementState.Parked;
        if (entry.Eligible != eligible || entry.Placement != window?.State || entry.PlacementGeneration != (window?.PlacementGeneration ?? -1))
        {
            entry.Eligible = eligible; entry.Placement = window?.State; entry.PlacementGeneration = window?.PlacementGeneration ?? -1;
            Invalidate(entry); Signal();
        }
        if (!live) { entry.Latest = null; entry.State = "Unavailable"; }
        else if (window!.State == PlacementState.Visible) { entry.Latest = null; entry.State = "ACTIVE"; }
        else if (!enabled || disposed) { entry.Latest = null; entry.State = "Stopped"; }
        else if (!eligible) { entry.Latest = null; entry.State = window.State.ToString(); }
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
            var reset = !enabled || options != requested;
            enabled = true; options = requested;
            foreach (var session in sessions.GetAll())
            {
                var entry = Sync(session.AppSessionId);
                if (reset || entry.Error is not null) Invalidate(entry);
            }
            Signal();
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
            using var output = new MemoryStream(); resized.Save(output, ImageFormat.Jpeg); jpeg = output.ToArray();
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
