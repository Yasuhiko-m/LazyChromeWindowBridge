namespace CallerHarness;

internal enum PlacementState { Visible, Parking, Parked, Restoring, Closed }
internal sealed record GeometrySnapshot(Guid AppSessionId, int? WindowId, string LaunchUrl, NativeIdentity Identity,
    PlacementState State, PixelRect Normal, PixelRect? Current, uint Dpi, string? Error);

internal sealed class GeometryCoordinator(SessionRegistry sessions, INativeWindows native, GeometryStore store, string chromeExecutable) : IDisposable
{
    private sealed class Entry(SessionSnapshot session, NativeIdentity identity, PixelRect normal)
    {
        public SessionSnapshot Session = session;
        public NativeIdentity Identity = identity;
        public PixelRect Normal = normal;
        public PlacementState State = PlacementState.Visible;
        public PixelRect? Candidate, Saved;
        public DateTimeOffset CandidateSince;
        public string? Error;
    }
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private bool disposed;
    public static string Marker(Guid id) => "LazyChromeExtension Session " + id.ToString("D");
    public MonitorGeometry[] Monitors() => native.Monitors();
    public GeometryProfile? Profile(string url) => store.Load(SessionRegistry.ValidateLaunchUrl(url));

    public bool EnsureMapped(Guid id)
    {
        lock (gate)
        {
            if (disposed) return false;
            if (entries.TryGetValue(id, out var existing)) return existing.State != PlacementState.Closed && native.Alive(existing.Identity);
            var session = sessions.Get(id);
            if (session?.State != SessionState.Bound) return false;
            var identity = native.FindAndTag(Marker(id), chromeExecutable);
            if (identity is null) return false; // Bootstrap title may not have reached the native window yet.
            try
            {
                var profile = store.Load(session.LaunchUrl);
                var normal = GeometryMath.VisibleFallback(profile?.Normal ?? native.Read(identity), native.Monitors());
                if (profile is not null || !native.Read(identity).Near(normal)) native.Move(identity, normal);
                var entry = new Entry(session, identity, native.Read(identity)) { Saved = profile?.Normal };
                entries.Add(id, entry);
                return true;
            }
            catch { native.Release(identity); throw; }
        }
    }
    private Entry Active(Guid id)
    {
        if (disposed || !entries.TryGetValue(id, out var entry) || entry.State == PlacementState.Closed ||
            sessions.Get(id)?.State != SessionState.Bound || !native.Alive(entry.Identity))
            throw new InvalidOperationException("Select a bound session with a live native window.");
        return entry;
    }
    private void Save(Entry entry, PixelRect normal)
    {
        if (entry.Saved != normal) store.Save(entry.Session.LaunchUrl, normal, native.Dpi(entry.Identity));
        entry.Saved = entry.Normal = normal;
        entry.Candidate = null;
    }
    public GeometrySnapshot? Get(Guid id)
    {
        lock (gate) return entries.TryGetValue(id, out var entry) ? Snapshot(entry) : null;
    }
    private GeometrySnapshot Snapshot(Entry entry)
    {
        var alive = native.Alive(entry.Identity);
        PixelRect? current = null;
        uint dpi = 0;
        try { if (alive) { current = native.Read(entry.Identity); dpi = native.Dpi(entry.Identity); } }
        catch (Exception error) { entry.Error = error.Message; alive = native.Alive(entry.Identity); }
        return new(entry.Session.AppSessionId, entry.Session.WindowId, entry.Session.LaunchUrl, entry.Identity,
            alive ? entry.State : PlacementState.Closed, entry.Normal, current, dpi, entry.Error);
    }
    public GeometrySnapshot Park(Guid id)
    {
        lock (gate)
        {
            var entry = Active(id);
            if (entry.State == PlacementState.Parked) return Snapshot(entry);
            var monitors = native.Monitors();
            var current = native.Read(entry.Identity);
            if (entry.State == PlacementState.Visible && native.Normal(entry.Identity) && GeometryMath.Reachable(current, monitors)) Save(entry, current);
            else Save(entry, entry.Normal);
            entry.State = PlacementState.Parking;
            try
            {
                native.Move(entry.Identity, GeometryMath.Park(entry.Normal, monitors));
                if (native.Monitors().Any(m => native.Read(entry.Identity).Intersects(m.Bounds))) throw new InvalidOperationException("PARK still intersects a monitor.");
                entry.State = PlacementState.Parked;
                entry.Error = null;
            }
            catch (Exception error) { entry.Error = error.Message; throw; } // Keep normal protected until explicit recovery.
            return Snapshot(entry);
        }
    }
    public GeometrySnapshot Restore(Guid id)
    {
        lock (gate)
        {
            var entry = Active(id);
            if (entry.State == PlacementState.Visible) return Snapshot(entry);
            entry.State = PlacementState.Restoring;
            try
            {
                var normal = GeometryMath.VisibleFallback(entry.Normal, native.Monitors());
                native.Move(entry.Identity, normal);
                Save(entry, native.Read(entry.Identity));
                entry.State = PlacementState.Visible;
                entry.Error = null;
            }
            catch (Exception error) { entry.Error = error.Message; throw; }
            return Snapshot(entry);
        }
    }
    // Same observation path for ordinary user movement and deterministic test movement.
    public void Poll(DateTimeOffset now)
    {
        lock (gate)
        {
            if (disposed) return;
            foreach (var entry in entries.Values.ToArray())
            {
                try
                {
                    if (sessions.Get(entry.Session.AppSessionId)?.State is SessionState.Closed or SessionState.Failed || !native.Alive(entry.Identity))
                    {
                        native.Release(entry.Identity);
                        entry.State = PlacementState.Closed;
                        continue;
                    }
                    if (entry.State == PlacementState.Closed) continue;
                    var monitors = native.Monitors();
                    var current = native.Read(entry.Identity);
                    if (entry.State == PlacementState.Parked)
                    {
                        if (monitors.Any(m => current.Intersects(m.Bounds))) native.Move(entry.Identity, GeometryMath.Park(entry.Normal, monitors));
                        continue;
                    }
                    if (entry.State != PlacementState.Visible || !native.Normal(entry.Identity)) continue;
                    if (!GeometryMath.Reachable(current, monitors)) { entry.Candidate = null; continue; }
                    if (current != entry.Candidate) { entry.Candidate = current; entry.CandidateSince = now; }
                    else if (now - entry.CandidateSince >= TimeSpan.FromMilliseconds(750)) Save(entry, current);
                    entry.Error = null;
                }
                catch (Exception error) { entry.Error = error.Message; }
            }
            foreach (var id in entries.Where(pair => pair.Value.State == PlacementState.Closed && sessions.Get(pair.Key) is null).Select(pair => pair.Key).ToArray()) entries.Remove(id);
        }
    }
    internal GeometrySnapshot MoveForTest(Guid id, PixelRect rect)
    {
        lock (gate)
        {
            var entry = Active(id);
            if (entry.State != PlacementState.Visible) throw new InvalidOperationException("Test movement requires a visible session.");
            native.Move(entry.Identity, rect);
            return Snapshot(entry);
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; // Serialize shutdown against pending UI operations and late bootstrap requests.
            foreach (var entry in entries.Values)
            {
                if (!native.Alive(entry.Identity)) continue;
                try
                {
                    if (entry.State is PlacementState.Parked or PlacementState.Parking or PlacementState.Restoring)
                        native.Move(entry.Identity, GeometryMath.VisibleFallback(entry.Normal, native.Monitors()));
                    else if (entry.State == PlacementState.Visible && native.Normal(entry.Identity) && GeometryMath.Reachable(native.Read(entry.Identity), native.Monitors()))
                        Save(entry, native.Read(entry.Identity));
                }
                catch (Exception error) { entry.Error = error.Message; }
                finally { native.Release(entry.Identity); }
            }
        }
    }
}
