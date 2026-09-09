using System.Runtime.CompilerServices;
using System.Reflection;
using LazyChromeWindowBridge.Core;

var count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
void Reject(Action action, string name)
{
    var rejected = false;
    try { action(); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, name);
}
var assembly = typeof(BridgeRuntime).Assembly;
Check(!assembly.GetReferencedAssemblies().Any(a => a.Name is "System.Windows.Forms" or "PresentationCore" or "PresentationFramework" or "WindowsBase"),
    "Core assembly has no WinForms/WPF UI dependency");
var friends = assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).ToArray();
Check(!friends.Contains("LazyChromeWindowBridge.SampleCaller") && !friends.Contains("LazyChromeWindowBridge.PublicApi.Tests"),
    "sample and consumer acceptance have no Core internal access");
Check(!assembly.GetExportedTypes().Any(t => t.Name is "SessionRegistry" or "GeometryCoordinator" or "MonitorCoordinator" or "NativeWindows"),
    "implementation coordinators remain internal");
Check(typeof(MonitorFrame).GetProperty(nameof(MonitorFrame.Jpeg))!.PropertyType == typeof(ReadOnlyMemory<byte>),
    "public frame data is encoded read-only memory, not a UI image");
Check(new CaptureOptions() is { FramesPerSecond: 2, MaxWidth: 240, MaxHeight: 135 }, "accepted capture defaults preserved");
Check(new PixelRect(-1820, 80, 1100, 720).Valid, "public physical rectangles allow negative coordinates");

var data = Path.Combine(Path.GetTempPath(), "LazyChromeWindowBridge-PublicApi-" + Guid.NewGuid().ToString("N"));
await using var bridge = await BridgeRuntime.StartAsync(new BridgeOptions("not-launched-by-this-test", null, data));
var unknown = Guid.NewGuid();
Check(bridge.GetSessions().Length == 0 && bridge.GetSession(unknown) is null, "public session enumeration and lookup");
Check(bridge.GetWindow(unknown) is null, "public unmapped Window query returns null");
Check(bridge.GetLatestFrame(unknown) is null, "public unknown session has no thumbnail");
Reject(() => bridge.SetWindowBounds(unknown, new PixelRect(40, 50, 800, 600)), "public Set cannot retarget an unmapped session");
Reject(() => bridge.Park(unknown), "public PARK rejects unmapped session");
Reject(() => bridge.Restore(unknown), "public RESTORE rejects unmapped session");
bridge.StartMonitoring(); bridge.StartMonitoring();
Check(bridge.GetMonitorState() is { Enabled: true, Connections: 0, CapturingConnections: 0 }, "public global Start is idempotent without targets");
bridge.StopMonitoring(); bridge.StopMonitoring();
Check(bridge.GetMonitorState() is { Enabled: false, Frames: 0, Bytes: 0 }, "public global Stop is idempotent");
await bridge.DisposeAsync(); await bridge.DisposeAsync();
Check(bridge.GetMonitorState() is { Connections: 0, CapturingConnections: 0 }, "public async disposal clears transport state");
var disposed = false;
try { await bridge.LaunchAsync("https://example.test/"); } catch (ObjectDisposedException) { disposed = true; }
Check(disposed, "disposed runtime cannot launch another session");
Reject(() => bridge.StartMonitoring(), "disposed runtime cannot restart capture");
Console.WriteLine($"PASS: {count} public API checks from an external consumer assembly.");
