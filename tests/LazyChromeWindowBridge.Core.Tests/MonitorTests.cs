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
        check(launch.ArgumentList.Count(a => a == "--disable-backgrounding-occluded-windows") == 1,
            "default Chrome launch includes exactly one offscreen rendering preservation flag");
        check(launch.ArgumentList.Contains("--no-first-run") && launch.ArgumentList.Contains("--no-default-browser-check") &&
            launch.ArgumentList.Contains("--new-window") && launch.ArgumentList.Contains("--user-data-dir=profile with spaces") && !launch.UseShellExecute,
            "Chrome launch retains flags and structured profile argument");
        check(new[] { "--user-data-dir=profile with spaces", "--no-first-run", "--no-default-browser-check", "--silent-debugger-extension-api", "--disable-backgrounding-occluded-windows", "--new-window" }
            .All(flag => launch.ArgumentList.Count(argument => argument == flag) == 1), "LCWB-managed Chrome arguments are not duplicated");
        check(launch.ArgumentList.Last() == bootstrap.AbsoluteUri, "Chrome launch retains complete bootstrap URI and fragment");
        var noBackgroundPreservation = ChromeLauncher.CreateStartInfo(new BridgeOptions("chrome.exe", "profile with spaces")
        {
            PreserveBackgroundRendering = false
        }, bootstrap);
        check(!noBackgroundPreservation.ArgumentList.Contains("--disable-backgrounding-occluded-windows") &&
            noBackgroundPreservation.ArgumentList.Where(a => a != "--disable-backgrounding-occluded-windows").SequenceEqual(launch.ArgumentList.Where(a => a != "--disable-backgrounding-occluded-windows")),
            "background rendering opt-out omits only the LCWB preservation flag");
        var callerArguments = new[] { "--load-extension=C:\\fixture extension", "--force-device-scale-factor=1.25" };
        var additional = ChromeLauncher.CreateStartInfo(new BridgeOptions("chrome.exe", "profile with spaces")
        {
            AdditionalChromeArguments = callerArguments
        }, bootstrap);
        var newWindow = additional.ArgumentList.IndexOf("--new-window");
        check(additional.ArgumentList.Count(a => a == callerArguments[0]) == 1 && additional.ArgumentList.Count(a => a == callerArguments[1]) == 1 &&
            additional.ArgumentList.IndexOf(callerArguments[0]) < additional.ArgumentList.IndexOf(callerArguments[1]) &&
            additional.ArgumentList.IndexOf(callerArguments[1]) < newWindow && additional.ArgumentList.Last() == bootstrap.AbsoluteUri,
            "caller Chrome switches preserve order before new-window and final bootstrap URI");
        var parsed = BridgeOptions.ParseCommandLine(["--chrome-argument", callerArguments[0], "--chrome-argument", callerArguments[1]]);
        check(parsed.AdditionalChromeArguments.SequenceEqual(callerArguments), "repeatable chrome-argument parsing preserves caller switch order");
        bool RejectChromeArgument(string argument)
        {
            try
            {
                ChromeLauncher.CreateStartInfo(new BridgeOptions("chrome.exe", null) { AdditionalChromeArguments = [argument] }, bootstrap);
                return false;
            }
            catch (ArgumentException) { return true; }
        }
        check(new[] { "--user-data-dir=caller", "--NEW-WINDOW", "--no-first-run=value", "--NO-DEFAULT-BROWSER-CHECK", "--silent-debugger-extension-api=1", "--DISABLE-BACKGROUNDING-OCCLUDED-WINDOWS" }
            .All(RejectChromeArgument), "reserved LCWB Chrome arguments are rejected case-insensitively");
        check(new[] { "", "profile", "--", "--flag\nvalue" }.All(RejectChromeArgument), "positional, empty and control-character Chrome arguments are rejected");
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
        var monitorGenerationBeforeKey = monitor.Control(a.AppSessionId).Generation;
        var keyDispatch = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.PageDown, BrowserKeyModifiers.Ctrl));
        var keyRequest = monitor.Control(a.AppSessionId).KeyChord;
        check(keyRequest is { Key: "PageDown" } && keyRequest.Modifiers.SequenceEqual(["Ctrl"]), "key chord control is canonical and session-local");
        monitor.KeyResult(a.AppSessionId, Guid.NewGuid(), keyRequest!.RequestId, true, null);
        check(!keyDispatch.IsCompleted, "wrong connection cannot acknowledge another session key chord");
        monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], keyRequest.RequestId, true, null);
        keyDispatch.GetAwaiter().GetResult();
        check(monitor.Control(a.AppSessionId).KeyChord is null && monitor.Control(a.AppSessionId).Generation == monitorGenerationBeforeKey && monitor.Latest(a.AppSessionId) is null,
            "key acknowledgement does not mutate monitor generation or latest frame");
        var first = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.Enter));
        check(Reject(() => monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.L)).GetAwaiter().GetResult()), "same-session key dispatch is bounded to one in-flight request");
        var firstRequest = monitor.Control(a.AppSessionId).KeyChord!;
        monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], firstRequest.RequestId, false, "dispatch rejected");
        check(Reject(() => first.GetAwaiter().GetResult()), "extension key-dispatch failure reaches the caller");
        var keyA = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.Enter, BrowserKeyModifiers.Ctrl));
        var keyB = monitor.SendKeyChordAsync(b.AppSessionId, new BrowserKeyChord(BrowserKey.R, BrowserKeyModifiers.Ctrl | BrowserKeyModifiers.Shift));
        var requestA = monitor.Control(a.AppSessionId).KeyChord!; var requestB = monitor.Control(b.AppSessionId).KeyChord!;
        check(requestA.RequestId != requestB.RequestId && requestB.Modifiers.SequenceEqual(["Ctrl", "Shift"]), "independent sessions serialize isolated modifier requests");
        monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], requestB.RequestId, true, null);
        check(!keyA.IsCompleted && !keyB.IsCompleted, "wrong request ID cannot cross-complete an independent session");
        monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], requestA.RequestId, true, null);
        monitor.KeyResult(b.AppSessionId, connections[b.AppSessionId], requestB.RequestId, true, null);
        Task.WaitAll(keyA, keyB);
        using (var cancelled = new CancellationTokenSource())
        {
            var cancelledDispatch = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.L, BrowserKeyModifiers.Ctrl), cancelled.Token);
            var cancelledRequest = monitor.Control(a.AppSessionId).KeyChord!;
            cancelled.Cancel(); var cancellationObserved = false;
            try { cancelledDispatch.GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancellationObserved = true; }
            check(cancellationObserved, "caller cancellation clears the in-flight key request");
            monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], cancelledRequest.RequestId, true, null);
            check(monitor.Control(a.AppSessionId).KeyChord is null, "late result cannot resurrect a cancelled key request");
        }
        var terminalRegistry = new SessionRegistry(); var terminalMonitor = new MonitorCoordinator(terminalRegistry, geometry);
        var keyUnbound = terminalRegistry.Create("https://example.test/unbound-key", DateTimeOffset.UtcNow).Session;
        var failed = terminalRegistry.Create("https://example.test/failed", DateTimeOffset.UtcNow).Session; terminalRegistry.FailLaunch(failed.AppSessionId, "fixture");
        var closed = terminalRegistry.Create("https://example.test/closed", DateTimeOffset.UtcNow).Session; terminalRegistry.Report(closed.AppSessionId, new(browser, 99, 199), true, DateTimeOffset.UtcNow);
        var disconnected = terminalRegistry.Create("https://example.test/disconnected", DateTimeOffset.UtcNow).Session;
        terminalRegistry.Report(disconnected.AppSessionId, new(browser, 98, 198), false, DateTimeOffset.UtcNow.AddSeconds(-101)); terminalRegistry.Sweep(DateTimeOffset.UtcNow);
        check(new[] { Guid.NewGuid(), keyUnbound.AppSessionId, failed.AppSessionId, closed.AppSessionId, disconnected.AppSessionId }.All(id =>
            Reject(() => terminalMonitor.SendKeyChordAsync(id, new BrowserKeyChord(BrowserKey.PageDown)).GetAwaiter().GetResult())),
            "unknown, unbound, failed, closed and disconnected sessions reject key dispatch");
        monitor.Disconnect(c.AppSessionId, connections[c.AppSessionId]);
        check(Reject(() => monitor.SendKeyChordAsync(c.AppSessionId, new BrowserKeyChord(BrowserKey.PageDown)).GetAwaiter().GetResult()),
            "live Bound session without an authenticated control connection rejects key dispatch");
        connections[c.AppSessionId] = Guid.NewGuid(); check(monitor.Connect(c.AppSessionId, connections[c.AppSessionId]), "disconnected key session can reconnect normally");
        var timeoutDispatch = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.PageUp));
        var timeoutRequest = monitor.Control(a.AppSessionId).KeyChord!; var timedOut = false;
        try { timeoutDispatch.GetAwaiter().GetResult(); } catch (TimeoutException) { timedOut = true; }
        check(timedOut && monitor.Control(a.AppSessionId).KeyChord is null, "five-second key timeout clears its in-flight request");
        monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], timeoutRequest.RequestId, true, null);
        check(monitor.Control(a.AppSessionId).KeyChord is null, "late timeout result cannot resurrect key completion");
        var sourceProbe = monitor.GetCaptureSourceSizeAsync(a.AppSessionId, CaptureMode.BrowserViewport).AsTask();
        var request = monitor.Control(a.AppSessionId).SourceSizeRequestId;
        check(request is { Length: 32 } && monitor.Latest(a.AppSessionId) is null,
            "BrowserViewport source-size request exists before Start and creates no frame");
        monitor.SourceSize(a.AppSessionId, connections[a.AppSessionId], request!, 1280, 800);
        check(sourceProbe.Result == new CaptureSourceSize(1280, 800), "BrowserViewport source-size accepts only the authenticated session response");
        monitor.Start(new());
        var before = monitor.Snapshot().Sessions.Select(s => s.Generation).ToArray(); monitor.Start(new());
        check(before.SequenceEqual(monitor.Snapshot().Sessions.Select(s => s.Generation)), "global Start is idempotent");
        check(monitor.Snapshot().Sessions.All(s => s.State == "Waiting") && rows.All(s => monitor.Control(s.AppSessionId).Enabled), "all live owned Visible sessions are capture eligible");
        string DataFor(CaptureOptions settings)
        {
            var (width, height) = CaptureSizing.OutputSize(1200, 800, settings);
            using var bitmap = new Bitmap(width, height); using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Firebrick);
            using var jpeg = new MemoryStream(); bitmap.Save(jpeg, ImageFormat.Jpeg);
            return Convert.ToBase64String(jpeg.ToArray());
        }
        void Frame(SessionSnapshot s, long? generation = null, int? window = null, Guid? connection = null, NativeIdentity? identity = null)
            => monitor.Accept(s.AppSessionId, connection ?? connections[s.AppSessionId], generation ?? monitor.Control(s.AppSessionId).Generation,
                window ?? s.WindowId!.Value, s.TabId!.Value, identity ?? geometry.Get(s.AppSessionId)!.Identity, DataFor(monitor.Control(s.AppSessionId).Options), 1);
        Frame(a);
        check(monitor.Latest(a.AppSessionId) is { Width: 202, Height: 135 } && monitor.Snapshot().Frames == 1,
            "Visible JPEG is accepted with aspect-preserving default bounds");
        var keyGeneration = monitor.Control(a.AppSessionId).Generation; var keyLatest = monitor.Latest(a.AppSessionId);
        var keyOptions = monitor.Control(a.AppSessionId).Options; var keyWindow = geometry.Get(a.AppSessionId);
        var stableDispatch = monitor.SendKeyChordAsync(a.AppSessionId, new BrowserKeyChord(BrowserKey.PageDown));
        var stableRequest = monitor.Control(a.AppSessionId).KeyChord!; monitor.KeyResult(a.AppSessionId, connections[a.AppSessionId], stableRequest.RequestId, true, null);
        stableDispatch.GetAwaiter().GetResult();
        check(monitor.Control(a.AppSessionId).Generation == keyGeneration && ReferenceEquals(monitor.Latest(a.AppSessionId), keyLatest) &&
            monitor.Control(a.AppSessionId).Options == keyOptions && geometry.Get(a.AppSessionId) == keyWindow && monitor.Control(a.AppSessionId).Enabled,
            "successful key dispatch preserves generation/latest/options/enabled state and exact geometry");
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
        RunNativeWindow(check);
    }

    private static void RunNativeWindow(Action<bool, string> check)
    {
        check(new CaptureOptions().Mode == CaptureMode.BrowserViewport, "CaptureOptions default remains BrowserViewport");
        var nativeOptions = new CaptureOptions(2, 240, 135, CaptureMode.NativeWindow); nativeOptions.Validate();
        check(nativeOptions.Mode == CaptureMode.NativeWindow, "explicit NativeWindow capture option validates");
        check(NativeWindowCaptureSession.OutputSize(1200, 800, 240, 135) == (202, 135) &&
            NativeWindowCaptureSession.OutputSize(80, 40, 240, 135) == (80, 40), "NativeWindow output is aspect-preserving and never upscaled");
        var bottomRight = new CaptureRegion(2, 2, 1, 1); bottomRight.Validate();
        check(CaptureSizing.RegionBounds(101, 99, bottomRight) == (50, 49, 51, 50) &&
            CaptureSizing.OutputSize(101, 99, new CaptureOptions { Region = bottomRight, Resize = new(80, 40) }) == (80, 40),
            "grid regions partition source pixels and explicit resize permits exact distortion");
        check(CaptureSizing.OutputSize(1200, 800, new CaptureOptions { Region = new(1, 2, 0, 1), Resize = new(300) }) == (300, 100) &&
            CaptureSizing.OutputSize(1200, 800, new CaptureOptions { Resize = new(null, 300) }) == (450, 300),
            "single explicit dimension preserves selected source aspect and permits upscale");
        var invalidGrid = false; var invalidResize = false;
        try { new CaptureOptions { Region = new(2, 2, 2, 0) }.Validate(); } catch (ArgumentException) { invalidGrid = true; }
        try { new CaptureOptions { Resize = new(0) }.Validate(); } catch (ArgumentException) { invalidResize = true; }
        check(invalidGrid && invalidResize, "invalid grid and explicit resize are rejected");
        check(!RejectNativeFilter(CaptureResizeFilter.Bilinear) && RejectNativeFilter(CaptureResizeFilter.NearestNeighbor) && RejectNativeFilter(CaptureResizeFilter.Bicubic),
            "NativeWindow explicitly accepts its D3D11 bilinear path and rejects unsupported nearest/bicubic filters");

        static bool RejectNativeFilter(CaptureResizeFilter filter)
        {
            try { D3D11Scaler.ValidateFilter(filter); return false; }
            catch (PlatformNotSupportedException) { return true; }
        }

        var registry = new SessionRegistry();
        var native = new GeometryTests.FakeNative([new("primary", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96, 96)]);
        var store = new GeometryStore(Path.Combine(Path.GetTempPath(), "LazyChromeWindowBridge-NativeMonitorTests-" + Guid.NewGuid().ToString("N")));
        using var geometry = new GeometryCoordinator(registry, native, store, "fake");
        var browser = Guid.NewGuid().ToString("D");
        SessionSnapshot Bind(int window)
        {
            var value = registry.Create("https://example.test/native/" + window, DateTimeOffset.UtcNow).Session;
            registry.Report(value.AppSessionId, new(browser, window, window + 100), false, DateTimeOffset.UtcNow);
            native.Add(GeometryCoordinator.Marker(value.AppSessionId), new(window, 300, window), new(20, 30, 1200, 800));
            geometry.EnsureMapped(value.AppSessionId);
            return registry.Get(value.AppSessionId)!;
        }
        var rows = new[] { Bind(31), Bind(32) };
        var factory = new FakeCaptureFactory();
        var monitor = new MonitorCoordinator(registry, geometry, factory);
        geometry.Changed += monitor.Reconcile;
        check(monitor.GetCaptureSourceSizeAsync(rows[0].AppSessionId, CaptureMode.NativeWindow).Result == new CaptureSourceSize(1200, 800) && monitor.Latest(rows[0].AppSessionId) is null,
            "NativeWindow source size is available before Start without a frame");
        monitor.Start(nativeOptions);
        check(SpinWait.SpinUntil(() => rows.All(row => monitor.Latest(row.AppSessionId) is not null), 3000),
            "independent NativeWindow peers publish bounded frames");
        var first = monitor.Latest(rows[0].AppSessionId)!;
        check(first is { Mode: CaptureMode.NativeWindow, TabId: -1, Width: 202, Height: 135 } && first.Identity == geometry.Get(rows[0].AppSessionId)!.Identity,
            "NativeWindow frame retains exact ownership and documents non-applicable TabId");
        using (var stream = new MemoryStream(first.Jpeg.ToArray()))
        using (var image = Image.FromStream(stream, false, true))
            check(image.RawFormat.Guid == ImageFormat.Jpeg.Guid && image.Width == first.Width && image.Height == first.Height,
                "NativeWindow frame is a valid bounded JPEG with truthful dimensions");
        check(rows.All(row => !monitor.Control(row.AppSessionId).Enabled) && factory.Opened.Distinct().SequenceEqual(rows.Select(row => geometry.Get(row.AppSessionId)!.Identity).OrderBy(i => i.Hwnd)),
            "NativeWindow uses exact HWND capture workers and never enables the CDP screenshot control path");

        var pollingGeneration = monitor.Control(rows[0].AppSessionId).Generation;
        var pollingSequence = first.Sequence; var readsBeforePolling = native.ReadCount; var openedBeforePolling = factory.Opened.Count;
        var pollingStable = true;
        for (var i = 0; i < 100; i++) { pollingStable &= ReferenceEquals(first, monitor.Latest(rows[0].AppSessionId)); _ = monitor.Snapshot(); }
        check(pollingStable && monitor.Latest(rows[0].AppSessionId) is { Generation: var latestGeneration, Sequence: var latestSequence } && latestGeneration == pollingGeneration && latestSequence == pollingSequence &&
            native.ReadCount == readsBeforePolling && factory.Opened.Count == openedBeforePolling,
            "Latest and monitor-state polling are observational and create no geometry/native-worker lifecycle transition");

        factory.BlockNextCapture(geometry.Get(rows[0].AppSessionId)!.Identity.Hwnd);
        check(SpinWait.SpinUntil(() => factory.CaptureBlocked, 3000), "native fixture blocks the next acquisition after a complete frame exists");
        var inFlightStable = true;
        for (var i = 0; i < 40; i++) inFlightStable &= ReferenceEquals(first, monitor.Latest(rows[0].AppSessionId)) && monitor.Latest(rows[0].AppSessionId)!.Sequence == pollingSequence;
        check(inFlightStable, "old complete NativeWindow frame remains published during GPU/capture replacement");
        factory.ReleaseBlockedCapture();
        check(SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId)?.Sequence > pollingSequence, 3000) &&
            monitor.Control(rows[0].AppSessionId).Generation == pollingGeneration,
            "native replacement publishes atomically once with unchanged generation and advancing sequence");
        first = monitor.Latest(rows[0].AppSessionId)!;

        var transientGeneration = monitor.Control(rows[0].AppSessionId).Generation; var transientOpened = factory.Opened.Count;
        native.FailNextReads = 1; monitor.Reconcile();
        check(ReferenceEquals(first, monitor.Latest(rows[0].AppSessionId)) && first.Generation == transientGeneration &&
            factory.Opened.Count == transientOpened && monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[0].AppSessionId).Identity == geometry.Get(rows[0].AppSessionId)!.Identity,
            "transient unreadable current rectangle preserves exact-owned NativeWindow monitor lifecycle");

        var peerFrames = monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[1].AppSessionId).Frames;
        monitor.SetSessionMonitoring(rows[0].AppSessionId, false);
        var frozen = monitor.Latest(rows[0].AppSessionId)!;
        check(SpinWait.SpinUntil(() => monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[0].AppSessionId) is { State: "Paused", Capturing: false }, 3000),
            "NativeWindow pause stops only its capture worker");
        check(SpinWait.SpinUntil(() => monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[1].AppSessionId).Frames > peerFrames, 1500) &&
            ReferenceEquals(frozen, monitor.Latest(rows[0].AppSessionId)),
            "NativeWindow pause freezes exact preview while peer continues");
        geometry.Park(rows[0].AppSessionId); geometry.Restore(rows[0].AppSessionId);
        check(ReferenceEquals(frozen, monitor.Latest(rows[0].AppSessionId)) && monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[0].AppSessionId).State == "Paused",
            "NativeWindow PARK/RESTORE does not resume a paused session");
        monitor.SetSessionMonitoring(rows[0].AppSessionId, true);
        check(SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId)?.Sequence > frozen.Sequence, 3000),
            "NativeWindow resume starts fresh capture for only the target");

        var generation = monitor.Control(rows[0].AppSessionId).Generation;
        monitor.Start(new(30, 640, 360, CaptureMode.NativeWindow));
        check(monitor.Control(rows[0].AppSessionId).Generation == generation &&
            SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId) is { Width: 540, Height: 360 }, 3000),
            "same-mode NativeWindow FPS and size update remains in place");
        var beforeRegion = monitor.Control(rows[0].AppSessionId).Generation;
        monitor.Start(new CaptureOptions(30, 640, 360, CaptureMode.NativeWindow) { Region = new(1, 2, 0, 1) });
        check(monitor.Control(rows[0].AppSessionId).Generation > beforeRegion &&
            SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId) is { Width: 640, Height: 213 }, 3000),
            "region change advances NativeWindow acquisition generation before publishing cropped pixels");
        var beforeTransition = monitor.Control(rows[0].AppSessionId).Generation;
        monitor.Start(new(2, 240, 135, CaptureMode.BrowserViewport));
        check(monitor.Control(rows[0].AppSessionId).Generation > beforeTransition && monitor.Latest(rows[0].AppSessionId) is null &&
            SpinWait.SpinUntil(() => monitor.Snapshot().CapturingConnections == 0, 3000),
            "mode transition advances acquisition generation, rejects late NativeWindow frames and releases workers");
        monitor.Start(nativeOptions);
        check(SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId) is not null, 3000), "global Start/update can re-enter NativeWindow capture");
        native.ReplaceIdentity(31, new(31, 999, 999)); monitor.Reconcile();
        check(SpinWait.SpinUntil(() => monitor.Latest(rows[0].AppSessionId) is null && !monitor.Snapshot().Sessions.Single(s => s.AppSessionId == rows[0].AppSessionId).Capturing, 3000),
            "stale/reused HWND identity stops NativeWindow capture without retargeting");
        check(monitor.Latest(rows[1].AppSessionId) is not null, "NativeWindow identity failure leaves independent peer running");
        monitor.Stop();
        check(SpinWait.SpinUntil(() => monitor.Snapshot().CapturingConnections == 0, 3000) && rows.All(row => monitor.Latest(row.AppSessionId) is null),
            "global Stop clears NativeWindow frames and reaches zero active captures");
        monitor.ShutdownAsync().GetAwaiter().GetResult();
        check(factory.Active == 0 && factory.Opened.Count >= 3, "NativeWindow shutdown deterministically disposes every acquisition resource");
    }

    private sealed class FakeCaptureFactory : INativeCaptureFactory
    {
        private readonly object gate = new();
        private readonly List<NativeIdentity> opened = [];
        private int active;
        private TaskCompletionSource? nextBlock, activeBlock;
        public bool CaptureBlocked => Volatile.Read(ref activeBlock) is not null;
        public IReadOnlyList<NativeIdentity> Opened { get { lock (gate) return opened.OrderBy(value => value.Hwnd).ToArray(); } }
        public int Active => Volatile.Read(ref active);
        public INativeCaptureSession Open(NativeIdentity identity)
        {
            lock (gate) opened.Add(identity);
            Interlocked.Increment(ref active);
            return new FakeCaptureSession(() => Interlocked.Decrement(ref active), token => WaitForCapture(identity.Hwnd, token));
        }
        private long nextBlockHwnd;
        public void BlockNextCapture(long hwnd)
        {
            lock (gate)
            {
                if (nextBlock is not null || activeBlock is not null) throw new InvalidOperationException("A capture block is already active.");
                nextBlockHwnd = hwnd; nextBlock = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        public void ReleaseBlockedCapture() => Interlocked.Exchange(ref activeBlock, null)?.TrySetResult();
        private async Task WaitForCapture(long hwnd, CancellationToken token)
        {
            TaskCompletionSource? wait;
            lock (gate)
            {
                if (nextBlockHwnd != hwnd) return;
                wait = nextBlock; nextBlock = null; nextBlockHwnd = 0;
            }
            if (wait is null) return;
            Interlocked.Exchange(ref activeBlock, wait);
            try { await wait.Task.WaitAsync(token); }
            finally { Interlocked.CompareExchange(ref activeBlock, null, wait); }
        }
    }
    private sealed class FakeCaptureSession(Action dispose, Func<CancellationToken, Task> waitForCapture) : INativeCaptureSession
    {
        private int disposed;
        public async Task<NativeCaptureFrame> CaptureAsync(CaptureOptions options, CancellationToken token)
        {
            await waitForCapture(token);
            await Task.Delay(15, token);
            var (width, height) = CaptureSizing.OutputSize(1200, 800, options);
            using var bitmap = new Bitmap(width, height); using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.DarkSlateBlue);
            using var output = new MemoryStream(); bitmap.Save(output, ImageFormat.Jpeg);
            return new(output.ToArray(), width, height, 2);
        }
        public ValueTask<CaptureSourceSize> GetSourceSizeAsync(CancellationToken token) => ValueTask.FromResult(new CaptureSourceSize(1200, 800));
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) dispose();
            return ValueTask.CompletedTask;
        }
    }
}
