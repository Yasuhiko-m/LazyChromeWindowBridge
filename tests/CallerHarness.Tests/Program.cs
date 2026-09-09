using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CallerHarness;

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
MonitorFrame? cachedFrame = null;
object? cachedEvidence = null;
json.Converters.Add(new JsonStringEnumConverter());
if (args.FirstOrDefault() == "--browser-driver")
{
    await using var host = await SessionHost.StartAsync(ChromeOptions.Parse(args[1..]));
    Console.WriteLine("R002 " + JsonSerializer.Serialize(new { ready = true }, json));
    while (await Console.In.ReadLineAsync() is { } line)
    {
        try
        {
            using var command = JsonDocument.Parse(line);
            var operation = command.RootElement.GetProperty("operation").GetString();
            if (operation == "stop") break;
            object? result = operation switch
            {
                "launch" => await host.LaunchAsync(command.RootElement.GetProperty("url").GetString()!),
                "sessions" => host.Sessions.GetAll(),
                "geometry" => host.Geometry.Get(command.RootElement.GetProperty("id").GetGuid())!,
                "park" => host.Geometry.Park(command.RootElement.GetProperty("id").GetGuid()),
                "restore" => host.Geometry.Restore(command.RootElement.GetProperty("id").GetGuid()),
                "move" => host.Geometry.MoveForTest(command.RootElement.GetProperty("id").GetGuid(), command.RootElement.GetProperty("rect").Deserialize<PixelRect>(json)!),
                "monitors" => host.Geometry.Monitors(),
                "profile" => host.Geometry.Profile(command.RootElement.GetProperty("url").GetString()!)!,
                "monitor-start" => StartMonitor(host, command.RootElement),
                "monitor-stop" => StopMonitor(host),
                "monitor" => host.Monitor.Snapshot(),
                "monitor-frame" => FrameEvidence(host.Monitor.Latest()),
                "monitor-image" => host.Monitor.Latest()?.Jpeg!,
                "process-stats" => ProcessStats(command.RootElement),
                "shutdown-host" => await ShutdownHost(host),
                _ => throw new ArgumentException("Unknown test-driver operation.")
            };
            Console.WriteLine("R002 " + JsonSerializer.Serialize(new { result }, json));
        }
        catch (Exception error) { Console.WriteLine("R002 " + JsonSerializer.Serialize(new { error = error.Message }, json)); }
    }
    return;
}

object StartMonitor(SessionHost host, JsonElement command)
{
    host.Monitor.Start(command.GetProperty("id").GetGuid(), command.TryGetProperty("options", out var options) ? options.Deserialize<CaptureOptions>(json)! : new());
    return host.Monitor.Snapshot();
}
object StopMonitor(SessionHost host) { host.Monitor.Stop(); return host.Monitor.Snapshot(); }
object? FrameEvidence(MonitorFrame? frame)
{
    if (frame is null) return null;
    if (ReferenceEquals(frame, cachedFrame)) return cachedEvidence;
    using var stream = new MemoryStream(frame.Jpeg);
    using var bitmap = new System.Drawing.Bitmap(stream);
    using var center = bitmap.Clone(new System.Drawing.Rectangle(bitmap.Width / 3, bitmap.Height / 3, bitmap.Width / 3, bitmap.Height / 3), bitmap.PixelFormat);
    using var pixels = new MemoryStream();
    center.Save(pixels, System.Drawing.Imaging.ImageFormat.Bmp);
    var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
    cachedFrame = frame;
    return cachedEvidence = new { frame.AppSessionId, frame.Identity, frame.WindowId, frame.TabId, frame.Generation, frame.Sequence,
        frame.Width, frame.Height, frame.ReceivedAt, Bytes = frame.Jpeg.Length, frame.CaptureMilliseconds,
        CenterPixel = new int[] { pixel.R, pixel.G, pixel.B },
        CenterHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels.ToArray())) };
}
async Task<object> ShutdownHost(SessionHost host) { await host.DisposeAsync(); return host.Monitor.Snapshot(); }
object ProcessStats(JsonElement command)
{
    using var caller = System.Diagnostics.Process.GetCurrentProcess();
    var processes = command.GetProperty("pids").EnumerateArray().Select(v => v.GetInt32()).Take(128).Distinct().ToArray();
    double cpu = 0; long working = 0, privateBytes = 0;
    foreach (var pid in processes)
    {
        try { using var process = System.Diagnostics.Process.GetProcessById(pid); cpu += process.TotalProcessorTime.TotalSeconds; working += process.WorkingSet64; privateBytes += process.PrivateMemorySize64; }
        catch (ArgumentException) { } catch (InvalidOperationException) { }
    }
    return new { CallerCpuSeconds = caller.TotalProcessorTime.TotalSeconds, CallerWorkingBytes = caller.WorkingSet64,
        CallerPrivateBytes = caller.PrivateMemorySize64, ChromeCpuSeconds = cpu, ChromeWorkingBytes = working, ChromePrivateBytes = privateBytes };
}

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
var registry = new SessionRegistry();
var now = DateTimeOffset.UtcNow;
var a = registry.Create("https://example.test/first", now);
var b = registry.Create("https://example.test/first", now);
var browserId = Guid.NewGuid().ToString("D");
var windowA = new WindowReport(browserId, 11, 101);
var windowB = new WindowReport(browserId, 12, 102);
Check(a.Session.AppSessionId != b.Session.AppSessionId && a.Token != b.Token, "unique identities even for identical launch URLs");
Check(registry.Authenticate(a.Session.AppSessionId, a.Token) && !registry.Authenticate(a.Session.AppSessionId, b.Token), "session capability isolation");
Check(registry.Report(a.Session.AppSessionId, windowA, false, now), "first bind");
Check(!registry.Report(b.Session.AppSessionId, windowA, false, now), "another session cannot claim an owned window");
Check(registry.Report(b.Session.AppSessionId, windowB, false, now), "concurrent distinct window");
Check(!registry.Report(a.Session.AppSessionId, windowB, false, now), "bound identity cannot migrate to another window");
registry.Sweep(now.AddSeconds(101));
Check(registry.Get(a.Session.AppSessionId)?.State == SessionState.Disconnected, "lost browser contact is explicit");
Check(registry.Report(a.Session.AppSessionId, windowA, false, now.AddSeconds(102)), "same binding can recover contact");
Check(registry.Report(a.Session.AppSessionId, windowA, true, now), "window close");
Check(registry.Report(a.Session.AppSessionId, windowA, true, now), "close delivery is idempotent");
Check(!registry.Report(a.Session.AppSessionId, windowA, false, now), "closed session cannot resurrect");
Check(registry.Get(b.Session.AppSessionId)?.WindowId == 12, "closing A does not change B");
var pending = registry.Create("http://127.0.0.1/test", now);
registry.Sweep(now.AddSeconds(31));
Check(registry.Get(pending.Session.AppSessionId)?.State == SessionState.Failed, "missing extension times out");
Check(!registry.Report(pending.Session.AppSessionId, windowA, false, now), "expired launch cannot bind late");
foreach (var bad in new[] { "javascript:alert(1)", "file:///C:/test", "https://user:password@example.test/", "not-a-url" })
{
    var rejected = false;
    try { SessionRegistry.ValidateLaunchUrl(bad); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "reject unsupported launch URL " + bad.Split(':')[0]);
}

await using (var host = await SessionHost.StartAsync(new ChromeOptions("unused-by-http-tests", null)))
{
    var launch = host.PrepareLaunch("https://example.test/one");
    var token = launch.Bootstrap.Fragment.Split("token=")[1];
    var endpoint = new Uri(host.BaseUri, SessionHost.Prefix + "/api/sessions/" + launch.Session.AppSessionId);
    using var client = new HttpClient();
    Check((await client.GetAsync(endpoint)).StatusCode == HttpStatusCode.Unauthorized, "HTTP rejects missing capability");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    Check((await client.GetAsync(endpoint)).IsSuccessStatusCode, "HTTP authenticated handshake");
    Check((await client.PostAsJsonAsync(endpoint + "/bind", windowA)).IsSuccessStatusCode, "HTTP bind contract");
    Check((await client.PostAsJsonAsync(endpoint + "/bind", windowB)).StatusCode == HttpStatusCode.Conflict, "HTTP rejects cross-binding");
    Check((await client.PostAsJsonAsync(endpoint + "/closed", windowA)).IsSuccessStatusCode, "HTTP close contract");
    Check((await client.PostAsJsonAsync(endpoint + "/bind", windowA)).StatusCode == HttpStatusCode.Conflict, "HTTP terminal close");
    using var wrongHost = new HttpRequestMessage(HttpMethod.Get, new Uri(host.BaseUri, SessionHost.Prefix + "/bootstrap"));
    wrongHost.Headers.Host = "untrusted.example";
    Check((await client.SendAsync(wrongHost)).StatusCode == HttpStatusCode.BadRequest, "reject non-loopback Host header");
    using var preflight = new HttpRequestMessage(HttpMethod.Options, endpoint);
    preflight.Headers.Add("Origin", "https://untrusted.example");
    preflight.Headers.Add("Access-Control-Request-Method", "POST");
    preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
    Check(!(await client.SendAsync(preflight)).Headers.Contains("Access-Control-Allow-Origin"), "no website CORS grant");
}
Console.WriteLine($"PASS: {count} caller/transport checks.");
var beforeGeometry = count;
GeometryTests.Run(Check);
Console.WriteLine($"PASS: {count - beforeGeometry} geometry checks; {count} total caller checks.");
var beforeMonitor = count;
MonitorTests.Run(Check);
Console.WriteLine($"PASS: {count - beforeMonitor} monitor checks; {count} total caller checks.");
