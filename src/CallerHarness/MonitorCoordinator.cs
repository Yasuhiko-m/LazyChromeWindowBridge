using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CallerHarness;

internal sealed record CaptureOptions(int FramesPerSecond = 2, int MaxWidth = 960, int MaxHeight = 540)
{
    public void Validate()
    {
        if (FramesPerSecond is < 1 or > 10 || MaxWidth is < 160 or > 1920 || MaxHeight is < 90 or > 1080)
            throw new ArgumentException("Monitor supports 1–10 fps and output up to 1920×1080.");
    }
}
internal sealed record MonitorControl(bool Enabled, long Generation, CaptureOptions Options, bool Closing);
internal sealed record MonitorFrame(Guid AppSessionId, NativeIdentity Identity, int WindowId, int TabId,
    long Generation, long Sequence, int Width, int Height, byte[] Jpeg, DateTimeOffset ReceivedAt, double CaptureMilliseconds);
internal sealed record MonitorSnapshot(Guid? AppSessionId, long Generation, string State, long Frames, long Bytes,
    double ElapsedSeconds, DateTimeOffset? LastFrameAt, int Width, int Height, string? Error, int Connections, int CapturingConnections);

internal sealed class MonitorCoordinator(SessionRegistry sessions, GeometryCoordinator geometry)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, (Guid Connection, bool Capturing)> connections = [];
    private Guid? selected;
    private long generation, frames, bytes;
    private CaptureOptions options = new();
    private MonitorFrame? latest;
    private string state = "Stopped";
    private string? error;
    private DateTimeOffset started = DateTimeOffset.UtcNow;
    private bool disposed;
    public void Start(Guid id, CaptureOptions requested)
    {
        requested.Validate();
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MonitorCoordinator));
            if (!Live(id)) throw new InvalidOperationException("Select a bound session with a live native window.");
            if (selected == id && requested == options && state != "Error") return;
            selected = id; options = requested; generation++; frames = bytes = 0; latest = null;
            error = null; state = "Waiting"; started = DateTimeOffset.UtcNow;
        }
    }
    public void Stop()
    {
        lock (gate)
        {
            if (selected is not null) generation++;
            selected = null; latest = null; state = "Stopped"; error = null;
        }
    }
    private bool Live(Guid id) => sessions.Get(id)?.State == SessionState.Bound && geometry.Get(id) is { State: not PlacementState.Closed, Current: not null };
    public bool Connect(Guid id, Guid connection)
    {
        lock (gate)
        {
            if (disposed || !Live(id) || connections.ContainsKey(id)) return false;
            connections[id] = (connection, false);
            return true;
        }
    }
    public void Disconnect(Guid id, Guid connection)
    {
        lock (gate)
        {
            if (!connections.TryGetValue(id, out var current) || current.Connection != connection) return;
            connections.Remove(id);
            if (selected == id && !disposed) { state = "Disconnected"; latest = null; }
        }
    }
    public MonitorControl Control(Guid id)
    {
        lock (gate) return new(!disposed && selected == id && state != "Error" && Live(id), generation, options, disposed);
    }
    public void Status(Guid id, Guid connection, long frameGeneration, string? failure, bool capturing)
    {
        lock (gate)
        {
            if (!connections.TryGetValue(id, out var current) || current.Connection != connection) return;
            connections[id] = (connection, capturing);
            if (selected != id || generation != frameGeneration || disposed) return;
            if (failure is not null) { state = "Error"; error = failure[..Math.Min(240, failure.Length)]; latest = null; }
        }
    }
    public void Accept(Guid id, Guid connection, long frameGeneration, int windowId, int tabId, string data, double captureMilliseconds)
    {
        CaptureOptions settings;
        lock (gate)
        {
            if (!Expected(id, connection, frameGeneration, windowId)) return;
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
            if (!Expected(id, connection, frameGeneration, windowId)) return; // Selection may change during decoding.
            var identity = geometry.Get(id)!.Identity;
            latest = new(id, identity, windowId, tabId, generation, ++frames, width, height, jpeg, DateTimeOffset.UtcNow, captureMilliseconds);
            bytes += data.Length; // Base64 transfer payload bytes, excluding small JSON/WebSocket overhead.
            state = "Live"; error = null;
            connections[id] = (connection, true);
        }
    }
    private bool Expected(Guid id, Guid connection, long frameGeneration, int windowId) => !disposed && selected == id && generation == frameGeneration &&
        connections.TryGetValue(id, out var current) && current.Connection == connection && Live(id) && sessions.Get(id)?.WindowId == windowId;
    public MonitorFrame? Latest()
    {
        lock (gate) return selected is { } id && Live(id) ? latest : null;
    }
    public MonitorSnapshot Snapshot()
    {
        lock (gate)
        {
            var displayState = selected is { } id && !Live(id) ? "Unavailable" : state;
            return new(selected, generation, displayState, frames, bytes, (DateTimeOffset.UtcNow - started).TotalSeconds,
                latest?.ReceivedAt, latest?.Width ?? 0, latest?.Height ?? 0, error, connections.Count, connections.Values.Count(c => c.Capturing));
        }
    }
    public async Task ShutdownAsync()
    {
        Stop();
        lock (gate) disposed = true;
        // The bridge remains alive briefly so extensions can detach before the listener closes.
        var until = DateTimeOffset.UtcNow.AddSeconds(3);
        while (Snapshot().Connections != 0 && DateTimeOffset.UtcNow < until) await Task.Delay(50);
    }
}
