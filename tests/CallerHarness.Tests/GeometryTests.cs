using CallerHarness;

internal static class GeometryTests
{
    public static void Run(Action<bool, string> check)
    {
        var primary = new MonitorGeometry("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96, 96);
        var left = new MonitorGeometry("left", new(-2560, -500, 2560, 1440), new(-2560, -500, 2560, 1400), false, 144, 144);
        MonitorGeometry[] monitors = [primary, left];
        var normal = new PixelRect(-2300, -350, 1000, 700);
        check(GeometryMath.Reachable(normal, monitors), "negative coordinates on a real-shaped secondary monitor are visible");
        check(GeometryMath.VisibleFallback(normal, monitors) == normal, "negative visible placement is preserved exactly");
        var park = GeometryMath.Park(normal, monitors);
        check(park.Top == -1264 && monitors.All(m => !park.Intersects(m.Bounds)), "PARK is above the topmost monitor by bounded margin");
        check(park.Width == normal.Width && park.Height == normal.Height, "PARK preserves physical size across DPI values");
        var fallback = GeometryMath.VisibleFallback(normal, [primary]);
        check(fallback == new PixelRect(0, 0, 1000, 700), "missing negative-coordinate monitor has deterministic primary fallback");
        var reduced = primary with { WorkArea = new(0, 60, 1200, 740), DpiX = 192, DpiY = 192 };
        check(GeometryMath.Reachable(GeometryMath.VisibleFallback(new(1800, 20, 1600, 1000), [reduced]), [reduced]), "changed work area retains reachable title bar and clamps size");
        check(GeometryMath.Park(normal, monitors) == GeometryMath.Park(normal, monitors.Select(m => m with { DpiX = 192, DpiY = 192 }).ToArray()), "physical geometry requires no Chrome DIP conversion or double scaling");
        check(!new PixelRect(0, -700, 1000, 700).Intersects(primary.Bounds), "edge-touching rectangles do not intersect");
        var temp = Path.Combine(Path.GetTempPath(), "LazyChromeExtension-R003-Unit-" + Guid.NewGuid().ToString("N"));
        var store = new GeometryStore(temp);
        var urlA = SessionRegistry.ValidateLaunchUrl("HTTPS://EXAMPLE.TEST:443/a?profile=1#first");
        var urlB = SessionRegistry.ValidateLaunchUrl("https://example.test/a?profile=2#first");
        check(urlA == "https://example.test/a?profile=1#first", "URI normalization only canonicalizes standard host/scheme/default port");
        store.Save(urlA, normal, 144);
        check(new GeometryStore(temp).Load(urlA)?.Normal == normal, "profile round-trip survives store recreation");
        check(store.Load(urlB) is null && store.Load(urlA.Replace("#first", "#second")) is null, "query and fragment keep distinct profile identities");
        var registry = new SessionRegistry();
        var native = new FakeNative(monitors);
        var now = DateTimeOffset.UtcNow;
        var browserId = Guid.NewGuid().ToString("D");
        SessionSnapshot Bind(string url, int window)
        {
            var session = registry.Create(url, now).Session;
            registry.Report(session.AppSessionId, new WindowReport(browserId, window, window + 100), false, now);
            native.Add(GeometryCoordinator.Marker(session.AppSessionId), new NativeIdentity(window, 200, window), new(60, 70, 900, 600));
            return registry.Get(session.AppSessionId)!;
        }
        var a = Bind(urlA, 11);
        var b = Bind(urlB, 12);
        using var coordinator = new GeometryCoordinator(registry, native, store, "fake-chrome");
        check(coordinator.EnsureMapped(a.AppSessionId) && coordinator.EnsureMapped(b.AppSessionId), "exact per-session marker selects distinct native identities");
        check(coordinator.Get(a.AppSessionId)?.Current == normal && coordinator.Get(b.AppSessionId)?.Current != normal, "startup restore applies only to the bound profile window");
        var moved = new PixelRect(-2200, -300, 1100, 760);
        coordinator.MoveForTest(a.AppSessionId, moved);
        coordinator.Poll(now);
        coordinator.Poll(now.AddMilliseconds(500));
        check(store.Load(urlA)?.Normal == normal, "resize observation is debounced before disk write");
        coordinator.Poll(now.AddMilliseconds(800));
        check(store.Load(urlA)?.Normal == moved, "stable ordinary movement persists normal geometry");
        var savedTime = store.Load(urlA)!.SavedAt;
        coordinator.Poll(now.AddSeconds(2)); coordinator.Poll(now.AddSeconds(3));
        check(store.Load(urlA)!.SavedAt == savedTime, "unchanged geometry does not rewrite the profile");
        var beforeB = coordinator.Get(b.AppSessionId)!;
        var parked = coordinator.Park(a.AppSessionId);
        check(parked.State == PlacementState.Parked && parked.Identity.Hwnd == 11 && monitors.All(m => !parked.Current!.Intersects(m.Bounds)), "PARK records explicit state and moves exact HWND outside every monitor");
        check(coordinator.Park(a.AppSessionId).Current == parked.Current, "duplicate PARK is idempotent");
        coordinator.Poll(now.AddSeconds(5)); coordinator.Poll(now.AddSeconds(7));
        check(store.Load(urlA)?.Normal == moved && coordinator.Get(a.AppSessionId)?.Normal == moved, "PARK never contaminates normal geometry or profile");
        check(coordinator.Get(b.AppSessionId)?.Current == beforeB.Current, "PARK A leaves concurrent B unchanged");
        var restored = coordinator.Restore(a.AppSessionId);
        check(restored.Current == moved && restored.LaunchUrl == urlA && restored.Identity == parked.Identity, "RESTORE retains profile and exact native ownership");
        check(coordinator.Restore(a.AppSessionId).Current == moved, "duplicate RESTORE is idempotent");
        coordinator.Park(a.AppSessionId);
        native.Topology = [primary];
        var fallbackRestore = coordinator.Restore(a.AppSessionId);
        check(fallbackRestore.State == PlacementState.Visible && GeometryMath.Reachable(fallbackRestore.Current!, [primary]), "monitor removal while PARKED restores to visible fallback");
        coordinator.Park(a.AppSessionId);
        var previousPark = coordinator.Get(a.AppSessionId)!.Current!;
        native.Topology = [primary, new("new-above", new(0, previousPark.Top - 20, 1920, 1080), new(0, previousPark.Top - 20, 1920, 1040), false, 120, 120)];
        coordinator.Poll(now.AddSeconds(8));
        check(native.Topology.All(m => !coordinator.Get(a.AppSessionId)!.Current!.Intersects(m.Bounds)), "new monitor covering PARK location causes deliberate re-PARK");
        native.Remove(11);
        registry.Report(a.AppSessionId, new WindowReport(browserId, 11, 111), true, now);
        coordinator.Poll(now.AddSeconds(9));
        check(coordinator.Get(a.AppSessionId)?.State == PlacementState.Closed, "window close while PARKED becomes terminal");
        var rejected = false;
        try { coordinator.Restore(a.AppSessionId); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && coordinator.Get(b.AppSessionId)?.Current == beforeB.Current, "closed HWND cannot be restored or replaced with B");
        native.ReplaceIdentity(12, new(12, 201, 999)); // Simulate HWND reuse after destruction.
        rejected = false;
        try { coordinator.Park(b.AppSessionId); } catch (InvalidOperationException) { rejected = true; }
        check(rejected, "PID/property identity mismatch rejects reused HWND");
        check(new GeometryStore(temp).Load(urlA)?.Normal != park, "persisted placement remains normal after parked close");
        var c = Bind("https://example.test/shutdown", 13);
        check(coordinator.EnsureMapped(c.AppSessionId), "shutdown fixture binds its own native window");
        var beforeShutdown = coordinator.Get(c.AppSessionId)!.Current;
        coordinator.Park(c.AppSessionId);
        coordinator.Dispose();
        check(coordinator.Get(c.AppSessionId)?.Current == beforeShutdown, "normal caller shutdown restores parked window");
        rejected = false;
        try { coordinator.Park(c.AppSessionId); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && !coordinator.EnsureMapped(c.AppSessionId), "shutdown rejects queued PARK and late native mapping");
        Console.WriteLine("Unit fixtures retained in temporary directory: " + temp);
    }
    internal sealed class FakeNative(MonitorGeometry[] monitors) : INativeWindows
    {
        private readonly Dictionary<string, long> markers = [];
        private readonly Dictionary<long, (NativeIdentity Identity, PixelRect Rect)> windows = [];
        public MonitorGeometry[] Topology = monitors;
        public void Add(string marker, NativeIdentity identity, PixelRect rect) { markers[marker] = identity.Hwnd; windows[identity.Hwnd] = (identity, rect); }
        public void Remove(long hwnd) => windows.Remove(hwnd);
        public void ReplaceIdentity(long hwnd, NativeIdentity identity) => windows[hwnd] = (identity, windows[hwnd].Rect);
        public NativeIdentity? FindAndTag(string marker, string chromeExecutable) => markers.TryGetValue(marker, out var hwnd) ? windows[hwnd].Identity : null;
        public bool Alive(NativeIdentity identity) => windows.TryGetValue(identity.Hwnd, out var window) && window.Identity == identity;
        public PixelRect Read(NativeIdentity identity) => Alive(identity) ? windows[identity.Hwnd].Rect : throw new InvalidOperationException("stale native identity");
        public bool Normal(NativeIdentity identity) => Alive(identity);
        public void Move(NativeIdentity identity, PixelRect rectangle) { _ = Read(identity); windows[identity.Hwnd] = (identity, rectangle); }
        public uint Dpi(NativeIdentity identity) => Topology.FirstOrDefault(m => Read(identity).Intersects(m.Bounds))?.DpiX ?? 96;
        public MonitorGeometry[] Monitors() => Topology;
        public void Release(NativeIdentity identity) { }
    }
}
