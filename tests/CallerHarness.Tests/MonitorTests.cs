using CallerHarness;
using System.Drawing;
using System.Drawing.Imaging;

internal static class MonitorTests
{
    public static void Run(Action<bool, string> check)
    {
        var registry = new SessionRegistry();
        var native = new GeometryTests.FakeNative([new("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96, 96)]);
        using var geometry = new GeometryCoordinator(registry, native, new GeometryStore(Path.Combine(Path.GetTempPath(), "LazyChromeExtension-R004-Unit-" + Guid.NewGuid().ToString("N"))), "fake");
        var browser = Guid.NewGuid().ToString("D");
        SessionSnapshot Bind(int window)
        {
            var s = registry.Create("https://example.test/" + window, DateTimeOffset.UtcNow).Session;
            registry.Report(s.AppSessionId, new(browser, window, window + 100), false, DateTimeOffset.UtcNow);
            native.Add(GeometryCoordinator.Marker(s.AppSessionId), new(window, 100, window), new(30, 40, 900, 600));
            geometry.EnsureMapped(s.AppSessionId);
            return registry.Get(s.AppSessionId)!;
        }
        var a = Bind(11); var b = Bind(12);
        var monitor = new MonitorCoordinator(registry, geometry);
        var connectionA = Guid.NewGuid(); var connectionB = Guid.NewGuid();
        check(monitor.Connect(a.AppSessionId, connectionA) && monitor.Connect(b.AppSessionId, connectionB), "monitor accepts exact live bound sessions");
        check(!monitor.Connect(a.AppSessionId, Guid.NewGuid()), "duplicate monitor socket cannot replace current session connection");
        monitor.Start(a.AppSessionId, new()); var generationA = monitor.Snapshot().Generation;
        monitor.Start(a.AppSessionId, new());
        check(monitor.Snapshot().Generation == generationA, "monitor start is idempotent for same target/options");
        using var bitmap = new Bitmap(1200, 800); using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Firebrick);
        using var jpeg = new MemoryStream(); bitmap.Save(jpeg, ImageFormat.Jpeg);
        var data = Convert.ToBase64String(jpeg.ToArray());
        monitor.Accept(a.AppSessionId, connectionA, generationA, 12, 111, data, 1);
        check(monitor.Latest() is null, "frame window mismatch is rejected");
        monitor.Accept(a.AppSessionId, Guid.NewGuid(), generationA, 11, 111, data, 1);
        check(monitor.Latest() is null, "frame from obsolete connection is rejected");
        monitor.Accept(a.AppSessionId, connectionA, generationA, 11, 111, data, 1);
        var frame = monitor.Latest()!;
        check(frame.AppSessionId == a.AppSessionId && frame.Identity.Hwnd == 11 && frame.Width <= 960 && frame.Height <= 540, "decoded frame preserves session identity and bounded preview dimensions");
        geometry.Park(a.AppSessionId);
        monitor.Accept(a.AppSessionId, connectionA, generationA, 11, 111, data, 1);
        check(monitor.Latest()!.Sequence == 2 && geometry.Get(a.AppSessionId)!.State == PlacementState.Parked, "PARK does not stop owned monitor acceptance");
        var normal = geometry.Get(a.AppSessionId)!.Normal;
        monitor.Status(a.AppSessionId, connectionA, generationA, "capture failed", false);
        check(monitor.Snapshot().State == "Error" && monitor.Latest() is null && !monitor.Control(a.AppSessionId).Enabled, "capture failure clears frame and stops capture control");
        check(registry.Get(a.AppSessionId)!.State == SessionState.Bound && geometry.Get(a.AppSessionId)!.Normal == normal, "capture failure cannot corrupt ownership or remembered geometry");
        monitor.Start(a.AppSessionId, new());
        check(monitor.Snapshot().Generation > generationA, "explicit retry starts a new monitor generation after failure");
        monitor.Start(b.AppSessionId, new()); var generationB = monitor.Snapshot().Generation;
        monitor.Accept(a.AppSessionId, connectionA, generationA, 11, 111, data, 1);
        check(monitor.Latest() is null && !monitor.Control(a.AppSessionId).Enabled && monitor.Control(b.AppSessionId).Enabled, "selection switch clears old frame and rejects late A pixels");
        monitor.Accept(b.AppSessionId, connectionB, generationB, 12, 112, data, 1);
        check(monitor.Latest()!.Identity.Hwnd == 12, "selected B preview carries B native identity");
        native.Remove(12); registry.Report(b.AppSessionId, new(browser, 12, 112), true, DateTimeOffset.UtcNow);
        check(monitor.Latest() is null && !monitor.Control(b.AppSessionId).Enabled, "closed selected window clears preview and disables capture");
        monitor.Stop(); var stopped = monitor.Snapshot().Generation; monitor.Stop();
        check(monitor.Snapshot().Generation == stopped && monitor.Latest() is null, "monitor stop is idempotent and clears image");
        monitor.Disconnect(a.AppSessionId, connectionA); monitor.Disconnect(b.AppSessionId, connectionB);
        monitor.ShutdownAsync().GetAwaiter().GetResult();
        check(monitor.Snapshot().Connections == 0 && monitor.Control(a.AppSessionId).Closing, "shutdown closes monitor control and releases connection accounting");
        var rejected = false;
        try { monitor.Start(a.AppSessionId, new()); } catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "monitor cannot restart after caller shutdown");
    }
}
