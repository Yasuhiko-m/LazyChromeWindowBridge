using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LazyChromeWindowBridge.Core;

internal static class DownloadTests
{
    internal static async Task Run(Action<bool, string> check)
    {
        await using var host = await BridgeRuntime.StartAsync(new BridgeOptions("unused-download-test", null,
            Path.Combine(Path.GetTempPath(), "LazyChromeWindowBridge-DownloadTests-" + Guid.NewGuid())));
        var first = host.PrepareLaunch("https://example.test/a");
        var second = host.PrepareLaunch("https://example.test/b");
        var profile = Guid.NewGuid();
        host.Sessions.Report(first.Session.AppSessionId, new(profile.ToString(), 11, 101), false, DateTimeOffset.UtcNow);
        host.Sessions.Report(second.Session.AppSessionId, new(profile.ToString(), 12, 102), false, DateTimeOffset.UtcNow);
        using var client = new HttpClient { BaseAddress = host.BaseUri };
        var events = new List<DownloadLifecycleEvent>();
        host.DownloadChanged += (_, _) => throw new InvalidOperationException("Consumer fixture exception");
        host.DownloadChanged += (sender, value) => { check(ReferenceEquals(sender, host), "download event sender is the public runtime"); events.Add(value); };
        var observed = DateTimeOffset.UtcNow;
        var created = new DownloadLifecycleEvent(1, DownloadLifecycleState.Created, "", null, observed);
        var report = new DownloadReport(host.BridgeId, profile, 1, created);
        async Task<HttpStatusCode> Send(DownloadReport body, bool useSecond = false, bool authenticate = true)
        {
            var binding = useSecond ? second : first;
            using var request = new HttpRequestMessage(HttpMethod.Post, BridgeRuntime.Prefix + "/api/downloads") { Content = JsonContent.Create(body) };
            if (authenticate)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", binding.Bootstrap.Fragment.Split("token=")[1]);
                request.Headers.Add("X-Bridge-Session", binding.Session.AppSessionId.ToString());
            }
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }
        check(await Send(report, authenticate: false) == HttpStatusCode.Unauthorized, "downloads require capability and representative header");
        check(await Send(report with { BridgeId = Guid.NewGuid() }) == HttpStatusCode.Conflict, "downloads reject wrong bridge identity");
        check(await Send(report with { BrowserSessionId = Guid.NewGuid() }) == HttpStatusCode.Conflict, "downloads reject wrong browser identity");
        check(await Send(report with { Sequence = 0 }) == HttpStatusCode.BadRequest, "downloads reject invalid sequence");
        check(await Send(report with { Event = created with { Filename = new string('x', 1025) } }) == HttpStatusCode.BadRequest, "downloads bound filename metadata");
        check(await Send(report) == HttpStatusCode.OK, "Created transport survives a throwing consumer");
        check(events.Count == 1 && events[0] == created, "Created retains exact official filename including initial empty value and observedAt");
        check((await Task.WhenAll(Send(report), Send(report, true))).All(s => s == HttpStatusCode.OK) && events.Count == 1,
            "concurrent duplicate retries and multiple capabilities emit once per bridge");
        const string filename = @"C:\Downloads\neutral-資料.zip";
        var complete = created with { State = DownloadLifecycleState.Complete, Filename = filename };
        check(await Send(report with { Sequence = 2, Event = complete }, true) == HttpStatusCode.OK, "Complete delivery accepts another representative of same bridge");
        check(events.Select(e => e.State).SequenceEqual([DownloadLifecycleState.Created, DownloadLifecycleState.Complete]), "download notification order is Created then Complete");
        check(host.GetDownload(1) == complete && host.GetDownloads().Length == 1, "public snapshots expose exact complete filename without file access");
        check(await Send(report with { Sequence = 3, Event = complete with { State = DownloadLifecycleState.Interrupted, Error = "NETWORK_FAILED" } }) == HttpStatusCode.OK && events.Count == 2,
            "Complete is final; late interrupted report cannot regress it");
        var interrupted = created with { DownloadId = 2, State = DownloadLifecycleState.Interrupted, Filename = @"C:\Downloads\failed.zip", Error = "NETWORK_FAILED" };
        check(await Send(report with { Sequence = 4, Event = interrupted }) == HttpStatusCode.OK && host.GetDownload(2) == interrupted,
            "Interrupted delivers exact filename and Chrome reason");
        check(events.Count == 3, "throwing subscriber does not prevent later notifications");
        var snapshot = host.GetDownloads(); snapshot[0] = interrupted;
        check(host.GetDownload(1) == complete, "consumer array mutation cannot mutate download state");
        check(typeof(DownloadLifecycleEvent).GetProperties().Select(p => p.Name).Order().SequenceEqual(
            new[] { "DownloadId", "State", "Filename", "Error", "ObservedAt" }.Order()), "public download payload has no session or URL attribution");
        using var bounded = new DownloadTracker(_ => { });
        for (var i = 0; i <= DownloadTracker.Capacity; i++) bounded.Receive(report with { Sequence = i + 1, Event = created with { DownloadId = i } });
        check(bounded.GetAll().Length == DownloadTracker.Capacity && bounded.Get(0) is null, "runtime snapshots retain only bounded latest observed downloads");
        bounded.Receive(report with { Event = created with { DownloadId = 0 } });
        check(bounded.Get(1)?.State == DownloadLifecycleState.Created && bounded.Get(0) is null && bounded.GetAll().Length == DownloadTracker.Capacity,
            "watermark survives snapshot eviction and rejects old retries");
        check(!bounded.Receive(report with { BrowserSessionId = Guid.NewGuid(), Sequence = 1000 }), "one configured profile identity per runtime");
        host.Sessions.Report(first.Session.AppSessionId, new(profile.ToString(), 11, 101), true, observed);
        check(await Send(report) == HttpStatusCode.Conflict, "closed session capability cannot deliver downloads");
        await host.DisposeAsync();
        check(host.GetDownloads().Length == 0 && host.GetDownload(1) is null, "normal async disposal clears download snapshots and subscribers");
        bounded.Dispose();
        check(!bounded.Receive(report with { Sequence = 1000 }) && bounded.GetAll().Length == 0, "disposed download tracker rejects later deliveries");
    }
}
