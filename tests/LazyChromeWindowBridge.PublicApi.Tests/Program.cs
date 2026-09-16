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
Check(new CaptureOptions() is { FramesPerSecond: 2, MaxWidth: 240, MaxHeight: 135, Mode: CaptureMode.BrowserViewport }, "accepted BrowserViewport capture defaults preserved");
var legacyCapture = new CaptureOptions(2, 240, 135);
var (legacyFps, legacyWidth, legacyHeight) = legacyCapture;
Check((legacyFps, legacyWidth, legacyHeight, legacyCapture.Mode) == (2, 240, 135, CaptureMode.BrowserViewport),
    "three-position CaptureOptions construction and deconstruction remain source compatible");
new CaptureOptions(2, 240, 135, CaptureMode.NativeWindow).Validate();
Check(Enum.GetValues<CaptureMode>().SequenceEqual([CaptureMode.BrowserViewport, CaptureMode.NativeWindow]), "public CaptureMode exposes only BrowserViewport and NativeWindow");
foreach (var fps in new[] { 1, 30 }) { new CaptureOptions(fps).Validate(); Check(true, $"public capture accepts {fps} fps request"); }
foreach (var fps in new[] { 0, 31 })
{
    var rejected = false;
    try { new CaptureOptions(fps).Validate(); } catch (ArgumentException) { rejected = true; }
    Check(rejected, $"public capture rejects {fps} fps");
}
Check(typeof(CaptureOptions).GetProperty("JpegQuality") is null, "JPEG quality is fixed rather than a public option");
var legacyBridgeOptions = new BridgeOptions("chrome.exe", null, "geometry");
Check(legacyBridgeOptions is { PreserveBackgroundRendering: true } && legacyBridgeOptions.AdditionalChromeArguments.Count == 0,
    "existing BridgeOptions construction remains valid with default launch policies");
var callerBridgeOptions = new BridgeOptions("chrome.exe", null)
{
    PreserveBackgroundRendering = false,
    AdditionalChromeArguments = ["--load-extension=C:\\fixture"]
};
Check(!callerBridgeOptions.PreserveBackgroundRendering && callerBridgeOptions.AdditionalChromeArguments.SequenceEqual(["--load-extension=C:\\fixture"]),
    "public BridgeOptions exposes background opt-out and caller Chrome switches");
Check(typeof(BridgeRuntime).GetMethod("SetSessionMonitoring", [typeof(Guid), typeof(bool)])?.ReturnType == typeof(void), "public session monitoring API is available without policy abstractions");
Check(typeof(BridgeRuntime).GetMethod("SetShowInTaskbar", [typeof(Guid), typeof(bool)])?.ReturnType == typeof(void), "public exact-session taskbar API is available");
Check(typeof(MonitorFrame).GetProperty(nameof(MonitorFrame.Mode))?.PropertyType == typeof(CaptureMode), "public frame metadata identifies its capture mode");
var region = new CaptureRegion(3, 3, 1, 1);
region.Validate();
var resize = new CaptureResize(320, null, CaptureResizeFilter.Bicubic); resize.Validate();
Check(new CaptureOptions { Region = region, Resize = resize }.Region == region && Enum.GetValues<CaptureResizeFilter>().SequenceEqual([CaptureResizeFilter.NearestNeighbor, CaptureResizeFilter.Bilinear, CaptureResizeFilter.Bicubic]),
    "public immutable grid region and explicit resize/filter contracts are externally consumable");
Check(typeof(BridgeRuntime).GetMethod("GetCaptureSourceSizeAsync", [typeof(Guid), typeof(CaptureMode), typeof(CancellationToken)])?.ReturnType == typeof(ValueTask<CaptureSourceSize>),
    "public pre-monitor capture source-size API is available");
Check(new PixelRect(-1820, 80, 1100, 720).Valid, "public physical rectangles allow negative coordinates");

var data = Path.Combine(Path.GetTempPath(), "LazyChromeWindowBridge-PublicApi-" + Guid.NewGuid().ToString("N"));
await using var bridge = await BridgeRuntime.StartAsync(new BridgeOptions("not-launched-by-this-test", null, data));
var unknown = Guid.NewGuid();
Check(bridge.GetSessions().Length == 0 && bridge.GetSession(unknown) is null, "public session enumeration and lookup");
Check(bridge.GetWindow(unknown) is null, "public unmapped Window query returns null");
Check(bridge.GetLatestFrame(unknown) is null, "public unknown session has no thumbnail");
Check(bridge.GetDownloads().Length == 0 && bridge.GetDownload(123) is null, "external consumer can query bounded download snapshots");
EventHandler<DownloadLifecycleEvent> downloadHandler = (_, value) => { _ = value.Filename; };
bridge.DownloadChanged += downloadHandler;
bridge.DownloadChanged -= downloadHandler;
Check(typeof(DownloadLifecycleEvent).GetProperty("AppSessionId") is null, "external download event is profile-global without guessed attribution");
Reject(() => bridge.SetWindowBounds(unknown, new PixelRect(40, 50, 800, 600)), "public Set cannot retarget an unmapped session");
Reject(() => bridge.Park(unknown), "public PARK rejects unmapped session");
Reject(() => bridge.Restore(unknown), "public RESTORE rejects unmapped session");
Reject(() => bridge.SetShowInTaskbar(unknown, false), "public taskbar API rejects an unmapped session");
bridge.StartMonitoring(); bridge.StartMonitoring();
Reject(() => bridge.SetSessionMonitoring(unknown, true), "public session ON rejects unknown session");
Reject(() => bridge.SetSessionMonitoring(unknown, false), "public session OFF rejects unknown session");
bridge.StartMonitoring(new(30, 640, 360));
Check(bridge.GetMonitorState() is { Enabled: true, Connections: 0, CapturingConnections: 0 }, "public global Start is idempotent without targets");
bridge.StartMonitoring(new(2, 240, 135, CaptureMode.NativeWindow));
Check(bridge.GetMonitorState() is { Enabled: true, Connections: 0, CapturingConnections: 0 } native && native.Sessions.Length == 0,
    "external consumer exercises NativeWindow mode without hidden targets or debugger work");
bridge.StopMonitoring(); bridge.StopMonitoring();
Reject(() => bridge.SetSessionMonitoring(unknown, true), "session control rejects global Stop without hidden start");
Check(bridge.GetMonitorState() is { Enabled: false, Frames: 0, Bytes: 0 }, "public global Stop is idempotent");
await bridge.DisposeAsync(); await bridge.DisposeAsync();
Check(bridge.GetMonitorState() is { Connections: 0, CapturingConnections: 0 }, "public async disposal clears transport state");
var disposed = false;
try { await bridge.LaunchAsync("https://example.test/"); } catch (ObjectDisposedException) { disposed = true; }
Check(disposed, "disposed runtime cannot launch another session");
Reject(() => bridge.StartMonitoring(), "disposed runtime cannot restart capture");
Reject(() => bridge.SetSessionMonitoring(unknown, true), "disposed runtime cannot resume session monitoring");
Console.WriteLine($"PASS: {count} public API checks from an external consumer assembly.");
