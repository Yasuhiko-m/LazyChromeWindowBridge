using LazyChromeWindowBridge.Core;
using System.Drawing;
using System.Drawing.Imaging;

internal static class MonitorTests
{
    public static void Run(Action<bool, string> check)
    {
        var bootstrap = new Uri("http://127.0.0.1:12345/lazy-chrome-window-bridge/bootstrap?session=fixture#fixture-capability");
        var launch = ChromeLauncher.CreateStartInfo(new BridgeOptions("chrome.exe", "profile with spaces"), bootstrap);
        check(launch.ArgumentList.Count(a => a == "--silent-debugger-extension-api") == 1, "Chrome launch includes exactly one silent debugger flag");
        check(launch.ArgumentList.Contains("--no-first-run") && launch.ArgumentList.Contains("--no-default-browser-check") &&
            launch.ArgumentList.Contains("--new-window") && launch.ArgumentList.Contains("--user-data-dir=profile with spaces") && !launch.UseShellExecute,
            "Chrome launch retains flags and structured profile argument");
        check(launch.ArgumentList.Last() == bootstrap.AbsoluteUri, "Chrome launch retains complete bootstrap URI and fragment");
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
        check(monitor.Snapshot().Sessions.All(s => s.State == "Waiting") && rows.All(s => monitor.Control(s.AppSessionId).Enabled), "all live owned Visible sessions are capture eligible");
        using var bitmap = new Bitmap(1200, 800); using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Firebrick);
        using var jpeg = new MemoryStream(); bitmap.Save(jpeg, ImageFormat.Jpeg);
        var data = Convert.ToBase64String(jpeg.ToArray());
        void Frame(SessionSnapshot s, long? generation = null, int? window = null, Guid? connection = null, NativeIdentity? identity = null)
            => monitor.Accept(s.AppSessionId, connection ?? connections[s.AppSessionId], generation ?? monitor.Control(s.AppSessionId).Generation,
                window ?? s.WindowId!.Value, s.TabId!.Value, identity ?? geometry.Get(s.AppSessionId)!.Identity, data, 1);
        Frame(a);
        check(monitor.Latest(a.AppSessionId) is { Width: 202, Height: 135 } && monitor.Snapshot().Frames == 1,
            "Visible JPEG is accepted with aspect-preserving default bounds");
        var visibleFrame = monitor.Latest(a.AppSessionId);
        var visibleGeneration = monitor.Control(a.AppSessionId).Generation;
        var visibleNative = geometry.Get(a.AppSessionId);
        monitor.Start(new(30, 960, 540));
        check(monitor.Control(a.AppSessionId) is { Options.FramesPerSecond: 30, Options.MaxWidth: 960, Options.MaxHeight: 540 } &&
            monitor.Control(a.AppSessionId).Generation == visibleGeneration && ReferenceEquals(monitor.Latest(a.AppSessionId), visibleFrame),
            "in-place options reach control without generation change or latest clear");
        Frame(a);
        check(monitor.Latest(a.AppSessionId) is { Width: 810, Height: 540 } && geometry.Get(a.AppSessionId) == visibleNative,
            "subsequent JPEG uses new bounds without native geometry change");
        var updatedFrame = monitor.Latest(a.AppSessionId);
        monitor.Start(new(1));
        check(monitor.Control(a.AppSessionId).Options.FramesPerSecond == 1 && monitor.Control(a.AppSessionId).Generation == visibleGeneration &&
            ReferenceEquals(monitor.Latest(a.AppSessionId), updatedFrame), "1 fps option accepted without restarting");
        monitor.Start(new());
        foreach (var s in rows.Skip(1)) geometry.Park(s.AppSessionId);
        check(rows.All(s => monitor.Control(s.AppSessionId).Enabled), "Visible and four PARKED targets are eligible concurrently");
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
        var beforeRestore = monitor.Latest(b.AppSessionId);
        var oldPlacementGeneration = geometry.Get(b.AppSessionId)!.PlacementGeneration;
        var otherGenerations = rows.Skip(2).Select(s => monitor.Control(s.AppSessionId).Generation).ToArray();
        geometry.Restore(b.AppSessionId);
        check(ReferenceEquals(monitor.Latest(b.AppSessionId), beforeRestore) && monitor.Control(b.AppSessionId).Enabled &&
            monitor.Control(b.AppSessionId).Generation == restoreGeneration && geometry.Get(b.AppSessionId)!.PlacementGeneration > oldPlacementGeneration &&
            monitor.Snapshot().Sessions.Single(s => s.AppSessionId == b.AppSessionId).State == "Live", "RESTORE retains latest JPEG and monitor generation despite placement generation change");
        check(otherGenerations.SequenceEqual(rows.Skip(2).Select(s => monitor.Control(s.AppSessionId).Generation)), "RESTORE does not change other session generations");
        geometry.Park(b.AppSessionId);
        check(ReferenceEquals(monitor.Latest(b.AppSessionId), beforeRestore) && monitor.Control(b.AppSessionId).Generation == restoreGeneration,
            "re-PARK keeps the same latest JPEG, generation and connection");
        Frame(b, generation: restoreGeneration);
        check(monitor.Latest(b.AppSessionId)!.Sequence > beforeRestore!.Sequence, "same-owned-window in-flight frame remains valid across placement changes");
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
        check(Reject(() => monitor.Start(new(0))) && Reject(() => monitor.Start(new(31))), "out-of-range fps rejected");
        check(monitor.Control(a.AppSessionId).Options == new CaptureOptions(), "invalid update does not alter existing options");
        check(Reject(() => monitor.Accept(a.AppSessionId, connections[a.AppSessionId], visibleGeneration, a.WindowId!.Value, a.TabId!.Value,
            geometry.Get(a.AppSessionId)!.Identity, Convert.ToBase64String([1, 2, 3, 4]), 1)), "invalid JPEG rejected before publication");
        using var tiny = new Bitmap(80, 40); using var tinyJpeg = new MemoryStream(); tiny.Save(tinyJpeg, ImageFormat.Jpeg);
        monitor.Accept(a.AppSessionId, connections[a.AppSessionId], visibleGeneration, a.WindowId!.Value, a.TabId!.Value,
            geometry.Get(a.AppSessionId)!.Identity, Convert.ToBase64String(tinyJpeg.ToArray()), 1);
        check(monitor.Latest(a.AppSessionId) is { Width: 80, Height: 40 }, "small capture is never upscaled");
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
        var frozen = monitor.Latest(a.AppSessionId)!;
        var pauseNative = geometry.Get(a.AppSessionId);
        var pauseGeneration = monitor.Control(a.AppSessionId).Generation;
        var peers = monitor.Snapshot().Sessions.Where(s => s.AppSessionId != a.AppSessionId).ToArray();
        var changed = monitor.Changed;
        monitor.SetSessionMonitoring(a.AppSessionId, false);
        check(changed.IsCompleted && !monitor.Control(a.AppSessionId).Enabled && rows.Skip(1).All(s => monitor.Control(s.AppSessionId).Enabled),
            "session OFF signals only target control disabled");
        monitor.Status(a.AppSessionId, connections[a.AppSessionId], pauseGeneration, null, false);
        check(monitor.Snapshot().Sessions.Single(s => s.AppSessionId == a.AppSessionId) is { State: "Paused", Capturing: false, Connected: true },
            "paused target stops capture while keeping its waiting connection");
        check(ReferenceEquals(frozen, monitor.Latest(a.AppSessionId)), "pause retains exact latest JPEG sequence and ReceivedAt");
        Frame(a, generation: pauseGeneration); Frame(a);
        check(ReferenceEquals(frozen, monitor.Latest(a.AppSessionId)), "paused target rejects late and current-generation frame publication");
        foreach (var s in rows.Skip(1)) Frame(s);
        check(peers.All(p => monitor.Snapshot().Sessions.Single(s => s.AppSessionId == p.AppSessionId) is var next &&
            next.Generation == p.Generation && next.Connected && next.State == "Live" && next.Frames > p.Frames), "peers remain Live with stable generations/connections during pause");
        check(geometry.Get(a.AppSessionId) == pauseNative && registry.Get(a.AppSessionId)!.State == SessionState.Bound,
            "session pause preserves native geometry and session ownership");
        var pausedGeneration = monitor.Control(a.AppSessionId).Generation;
        monitor.SetSessionMonitoring(a.AppSessionId, false);
        check(monitor.Control(a.AppSessionId).Generation == pausedGeneration, "repeated session OFF is idempotent");
        geometry.Park(a.AppSessionId); geometry.Restore(a.AppSessionId);
        check(monitor.Snapshot().Sessions.Single(s => s.AppSessionId == a.AppSessionId).State == "Paused" &&
            !monitor.Control(a.AppSessionId).Enabled && monitor.Control(a.AppSessionId).Generation == pausedGeneration &&
            ReferenceEquals(frozen, monitor.Latest(a.AppSessionId)), "PARK/RESTORE never auto-resumes paused frozen preview");
        monitor.Start(new(30, 640, 360));
        check(!monitor.Control(a.AppSessionId).Enabled && monitor.Control(a.AppSessionId).Generation == pausedGeneration &&
            ReferenceEquals(frozen, monitor.Latest(a.AppSessionId)), "global in-place options preserve explicit session OFF and frozen JPEG");
        monitor.SetSessionMonitoring(a.AppSessionId, true);
        check(monitor.Control(a.AppSessionId).Enabled && monitor.Snapshot().Sessions.Single(s => s.AppSessionId == a.AppSessionId) is { State: "Waiting", Connected: true },
            "session ON resumes Waiting with existing control connection");
        Frame(a);
        check(monitor.Latest(a.AppSessionId)!.Sequence > frozen.Sequence && monitor.Snapshot().Sessions.Single(s => s.AppSessionId == a.AppSessionId).State == "Live",
            "session resume publishes fresh frames with advancing sequence");
        check(peers.All(p => monitor.Snapshot().Sessions.Single(s => s.AppSessionId == p.AppSessionId).Generation == p.Generation), "session resume leaves peer generations unchanged");
        var resumedGeneration = monitor.Control(a.AppSessionId).Generation;
        monitor.SetSessionMonitoring(a.AppSessionId, true);
        check(monitor.Control(a.AppSessionId).Generation == resumedGeneration, "healthy session ON is idempotent");
        monitor.Status(a.AppSessionId, connections[a.AppSessionId], resumedGeneration, "real capture error", false);
        monitor.SetSessionMonitoring(a.AppSessionId, true); Frame(a);
        check(monitor.Control(a.AppSessionId).Generation > resumedGeneration && monitor.Latest(a.AppSessionId) is not null,
            "explicit session ON retries real error in a new target generation");
        check(Reject(() => monitor.SetSessionMonitoring(Guid.NewGuid(), true)), "unknown session monitoring rejected");
        var unbound = registry.Create("https://example.test/unbound", DateTimeOffset.UtcNow).Session;
        check(Reject(() => monitor.SetSessionMonitoring(unbound.AppSessionId, false)), "unbound session monitoring rejected");
        monitor.SetSessionMonitoring(a.AppSessionId, false); monitor.Stop();
        check(rows.All(s => monitor.Latest(s.AppSessionId) is null && !monitor.Control(s.AppSessionId).Enabled), "global Stop clears paused and live JPEGs");
        check(Reject(() => monitor.SetSessionMonitoring(a.AppSessionId, true)) && Reject(() => monitor.SetSessionMonitoring(a.AppSessionId, false)) && !monitor.Snapshot().Enabled,
            "session API rejects global stopped state without hidden start");
        monitor.Start(new());
        check(rows.All(s => monitor.Control(s.AppSessionId).Enabled), "Stop then global Start batch-enables previously paused live sessions");
        native.ReplaceIdentity(14, new(14, 201, 999));
        check(!monitor.Control(rows[3].AppSessionId).Enabled && monitor.Latest(rows[3].AppSessionId) is null, "reused HWND/PID/tag makes monitor unavailable");
        check(Reject(() => monitor.SetSessionMonitoring(rows[3].AppSessionId, true)), "session ON rejects stale native identity");
        check(Reject(() => geometry.SetWindowBounds(rows[3].AppSessionId, set)), "manual Set cannot move reused HWND");
        native.Remove(15); registry.Report(rows[4].AppSessionId, new(browser, 15, 115), true, now);
        check(!monitor.Control(rows[4].AppSessionId).Enabled && monitor.Latest(rows[4].AppSessionId) is null, "closed session has no stale pixels or capture");
        check(Reject(() => monitor.SetSessionMonitoring(rows[4].AppSessionId, false)), "session OFF rejects closed owner");
        check(Reject(() => geometry.SetWindowBounds(rows[4].AppSessionId, set)), "manual Set rejects closed identity");
        monitor.Stop(); var stopped = monitor.Snapshot().Sessions.Select(s => s.Generation).ToArray(); monitor.Stop();
        check(stopped.SequenceEqual(monitor.Snapshot().Sessions.Select(s => s.Generation)) && rows.All(s => monitor.Latest(s.AppSessionId) is null), "global Stop is idempotent and clears all thumbnails");
        Frame(b, generation: restoreGeneration);
        check(monitor.Latest(b.AppSessionId) is null && !monitor.Control(b.AppSessionId).Enabled, "Stop rejects stale in-flight frames");
        foreach (var s in rows) monitor.Disconnect(s.AppSessionId, connections[s.AppSessionId]);
        monitor.ShutdownAsync().GetAwaiter().GetResult();
        check(monitor.Snapshot().Connections == 0 && monitor.Control(b.AppSessionId).Closing, "normal monitor shutdown clears resources");
        geometry.Dispose();
        check(native.Read(geometry.Get(b.AppSessionId)!.Identity) == normal && store.Load(b.LaunchUrl)!.Normal == normal, "normal shutdown restores protected Normal after small PARK");
        check(Reject(() => monitor.Start(new())), "monitor cannot restart after shutdown");
        check(Reject(() => monitor.SetSessionMonitoring(a.AppSessionId, true)), "session monitor cannot resume after shutdown");
    }
}
