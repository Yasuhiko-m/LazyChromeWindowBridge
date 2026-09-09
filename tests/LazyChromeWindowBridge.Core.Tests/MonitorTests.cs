using LazyChromeWindowBridge.Core;
using System.Drawing;
using System.Drawing.Imaging;

internal static class MonitorTests
{
    public static void Run(Action<bool, string> check)
    {
        var registry = new SessionRegistry();
        var native = new GeometryTests.FakeNative([new("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96, 96),
            new("left", new(-1920, 0, 1920, 1080), new(-1920, 0, 1920, 1040), false, 96, 96)]);
        var store = new GeometryStore(Path.Combine(Path.GetTempPath(), "LazyChromeWindowBridge-MonitorTests-" + Guid.NewGuid().ToString("N")));
        using var geometry = new GeometryCoordinator(registry, native, store, "fake");
        var browser = Guid.NewGuid().ToString("D");
        SessionSnapshot Bind(int window)
        {
            var s = registry.Create("https://example.test/" + window, DateTimeOffset.UtcNow).Session;
            registry.Report(s.AppSessionId, new(browser, window, window + 100), false, DateTimeOffset.UtcNow);
            native.Add(GeometryCoordinator.Marker(s.AppSessionId), new(window, 100, window), new(30, 40, 1280, 800));
            geometry.EnsureMapped(s.AppSessionId);
            return registry.Get(s.AppSessionId)!;
        }
        var rows = Enumerable.Range(11, 5).Select(Bind).ToArray();
        var a = rows[0]; var b = rows[1]; var c = rows[2];
        var monitor = new MonitorCoordinator(registry, geometry);
        geometry.Changed += monitor.Reconcile;
        var connections = rows.ToDictionary(s => s.AppSessionId, _ => Guid.NewGuid());
        foreach (var s in rows) check(monitor.Connect(s.AppSessionId, connections[s.AppSessionId]), "independent connection " + s.WindowId);
        check(!monitor.Connect(a.AppSessionId, Guid.NewGuid()), "duplicate socket cannot replace an owned connection");
        monitor.Start(new());
        var before = monitor.Snapshot().Sessions.Select(s => s.Generation).ToArray(); monitor.Start(new());
        check(before.SequenceEqual(monitor.Snapshot().Sessions.Select(s => s.Generation)), "global Start is idempotent");
        check(monitor.Snapshot().Sessions.All(s => s.State == "ACTIVE") && rows.All(s => !monitor.Control(s.AppSessionId).Enabled), "Visible sessions are ACTIVE with no capture request");
        using var bitmap = new Bitmap(1200, 800); using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Firebrick);
        using var jpeg = new MemoryStream(); bitmap.Save(jpeg, ImageFormat.Jpeg);
        var data = Convert.ToBase64String(jpeg.ToArray());
        void Frame(SessionSnapshot s, long? generation = null, int? window = null, Guid? connection = null, NativeIdentity? identity = null)
            => monitor.Accept(s.AppSessionId, connection ?? connections[s.AppSessionId], generation ?? monitor.Control(s.AppSessionId).Generation,
                window ?? s.WindowId!.Value, s.TabId!.Value, identity ?? geometry.Get(s.AppSessionId)!.Identity, data, 1);
        Frame(a);
        check(monitor.Latest(a.AppSessionId) is null && monitor.Snapshot().Frames == 0, "Visible frame traffic is rejected");
        foreach (var s in rows.Skip(1)) geometry.Park(s.AppSessionId);
        check(rows.Skip(1).All(s => monitor.Control(s.AppSessionId).Enabled) && !monitor.Control(a.AppSessionId).Enabled, "four PARKED targets requested concurrently while ACTIVE remains disabled");
        Frame(b, window: c.WindowId); Frame(b, connection: Guid.NewGuid()); Frame(b, identity: new(12, 100, 999));
        check(monitor.Latest(b.AppSessionId) is null, "wrong window, connection and native property identity rejected");
        foreach (var s in rows.Skip(1)) Frame(s);
        check(rows.Skip(1).All(s => monitor.Latest(s.AppSessionId) is { Width: <= 240, Height: <= 135 } f && f.Identity == geometry.Get(s.AppSessionId)!.Identity),
            "four independent bounded thumbnails retain exact native identity");
        var oldGeneration = monitor.Control(b.AppSessionId).Generation;
        var normal = geometry.Get(b.AppSessionId)!.Normal;
        monitor.Status(b.AppSessionId, connections[b.AppSessionId], oldGeneration, "capture failed", false);
        Frame(b);
        check(monitor.Latest(b.AppSessionId) is null && !monitor.Control(b.AppSessionId).Enabled, "failure blocks later frames in the same generation");
        check(rows.Skip(2).All(s => monitor.Control(s.AppSessionId).Enabled && monitor.Latest(s.AppSessionId) is not null), "one capture failure leaves three others running");
        check(registry.Get(b.AppSessionId)!.State == SessionState.Bound && geometry.Get(b.AppSessionId)!.Normal == normal, "failure cannot corrupt binding or Normal");
        monitor.Start(new()); Frame(b);
        check(monitor.Control(b.AppSessionId).Generation > oldGeneration && monitor.Latest(b.AppSessionId) is not null, "explicit Start retries the failed generation");
        var restoreGeneration = monitor.Control(b.AppSessionId).Generation;
        var otherGenerations = rows.Skip(2).Select(s => monitor.Control(s.AppSessionId).Generation).ToArray();
        geometry.Restore(b.AppSessionId);
        check(monitor.Latest(b.AppSessionId) is null && !monitor.Control(b.AppSessionId).Enabled &&
            monitor.Snapshot().Sessions.Single(s => s.AppSessionId == b.AppSessionId).State == "ACTIVE", "RESTORE immediately clears pixels and disables its capture");
        check(otherGenerations.SequenceEqual(rows.Skip(2).Select(s => monitor.Control(s.AppSessionId).Generation)), "RESTORE does not change other session generations");
        geometry.Park(b.AppSessionId); Frame(b, generation: restoreGeneration);
        check(monitor.Latest(b.AppSessionId) is null, "late pre-RESTORE frame rejected after re-PARK");
        Frame(b); check(monitor.Latest(b.AppSessionId) is not null, "re-PARK reacquires the same session");
        var oldConnection = connections[c.AppSessionId]; var oldCGeneration = monitor.Control(c.AppSessionId).Generation;
        monitor.Disconnect(c.AppSessionId, oldConnection);
        connections[c.AppSessionId] = Guid.NewGuid(); monitor.Connect(c.AppSessionId, connections[c.AppSessionId]);
        Frame(c, generation: oldCGeneration, connection: oldConnection);
        check(monitor.Latest(c.AppSessionId) is null, "worker reconnection rejects obsolete connection/generation");
        Frame(c); check(monitor.Latest(c.AppSessionId) is not null, "worker reconnection retains capture target");
        var other = geometry.Get(c.AppSessionId)!.Current;
        var set = new PixelRect(-1800, 70, 1000, 700);
        var moved = geometry.SetWindowBounds(a.AppSessionId, set);
        check(moved.Current == set && moved.Normal != set && moved.Dpi == 96 && moved.LaunchUrl == a.LaunchUrl && moved.WindowId == a.WindowId,
            "manual Get/Set exposes physical negative coordinates, identity, DPI and retained Normal");
        check(geometry.Get(c.AppSessionId)!.Current == other, "manual Set isolates another session");
        var now = DateTimeOffset.UtcNow;
        geometry.Poll(now); geometry.Poll(now.AddSeconds(1));
        check(store.Load(a.LaunchUrl)!.Normal == set, "manual bounds use existing stable observer persistence");
        bool Reject(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } catch (ArgumentException) { return true; } }
        check(Reject(() => geometry.SetWindowBounds(b.AppSessionId, set)), "manual Set requires RESTORE for PARKED sessions");
        check(Reject(() => geometry.SetWindowBounds(a.AppSessionId, new(0, -5000, 900, 600))), "manual Set rejects unreachable placement");
        check(Reject(() => geometry.SetWindowBounds(Guid.NewGuid(), set)), "manual Set rejects unmapped session");
        geometry.ResizeParkedForTest(b.AppSessionId, 512, 320);
        geometry.Poll(now.AddSeconds(2)); geometry.Poll(now.AddSeconds(4));
        check(geometry.Get(b.AppSessionId)!.Normal == normal && store.Load(b.LaunchUrl)!.Normal == normal, "small PARK dimensions cannot contaminate remembered or persisted Normal");
        var parked = geometry.Get(b.AppSessionId)!.Current!;
        native.Topology = [new("new-above", new(0, parked.Top - 20, 1920, 1080), new(0, parked.Top - 20, 1920, 1040), true, 96, 96)];
        geometry.Poll(now.AddSeconds(5));
        check(geometry.Get(b.AppSessionId)!.Current is { Width: 512, Height: 320 } reparking && !reparking.Intersects(native.Topology[0].Bounds), "topology re-PARK preserves small test dimensions and stays outside");
        native.Topology = [new("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96, 96)];
        check(geometry.Restore(b.AppSessionId).Current == normal, "RESTORE ignores small PARK dimensions");
        geometry.Park(b.AppSessionId); geometry.ResizeParkedForTest(b.AppSessionId, 512, 320);
        native.ReplaceIdentity(14, new(14, 201, 999));
        check(!monitor.Control(rows[3].AppSessionId).Enabled && monitor.Latest(rows[3].AppSessionId) is null, "reused HWND/PID/tag makes monitor unavailable");
        check(Reject(() => geometry.SetWindowBounds(rows[3].AppSessionId, set)), "manual Set cannot move reused HWND");
        native.Remove(15); registry.Report(rows[4].AppSessionId, new(browser, 15, 115), true, now);
        check(!monitor.Control(rows[4].AppSessionId).Enabled && monitor.Latest(rows[4].AppSessionId) is null, "closed session has no stale pixels or capture");
        check(Reject(() => geometry.SetWindowBounds(rows[4].AppSessionId, set)), "manual Set rejects closed identity");
        monitor.Stop(); var stopped = monitor.Snapshot().Sessions.Select(s => s.Generation).ToArray(); monitor.Stop();
        check(stopped.SequenceEqual(monitor.Snapshot().Sessions.Select(s => s.Generation)) && rows.All(s => monitor.Latest(s.AppSessionId) is null), "global Stop is idempotent and clears all thumbnails");
        foreach (var s in rows) monitor.Disconnect(s.AppSessionId, connections[s.AppSessionId]);
        monitor.ShutdownAsync().GetAwaiter().GetResult();
        check(monitor.Snapshot().Connections == 0 && monitor.Control(b.AppSessionId).Closing, "normal monitor shutdown clears resources");
        geometry.Dispose();
        check(native.Read(geometry.Get(b.AppSessionId)!.Identity) == normal && store.Load(b.LaunchUrl)!.Normal == normal, "normal shutdown restores protected Normal after small PARK");
        check(Reject(() => monitor.Start(new())), "monitor cannot restart after shutdown");
    }
}
