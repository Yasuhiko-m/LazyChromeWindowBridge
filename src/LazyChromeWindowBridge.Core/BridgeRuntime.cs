using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LazyChromeWindowBridge.Core;

public sealed class BridgeRuntime : IAsyncDisposable
{
    internal const string Prefix = "/lazy-chrome-window-bridge";
    private readonly WebApplication server;
    private readonly BridgeOptions chrome;
    private readonly System.Threading.Timer expiryTimer;
    private int disposed;
    internal SessionRegistry Sessions { get; } = new();
    internal GeometryCoordinator Geometry { get; }
    internal MonitorCoordinator Monitor { get; }
    internal Guid BridgeId { get; } = Guid.NewGuid();
    internal Uri BaseUri { get; private set; } = null!;
    private BridgeRuntime(WebApplication server, BridgeOptions chrome)
    {
        this.server = server;
        this.chrome = chrome;
        Geometry = new GeometryCoordinator(Sessions, new NativeWindows(), new GeometryStore(chrome.GeometryDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LazyChromeWindowBridge", "Geometry")), chrome.Executable);
        Monitor = new MonitorCoordinator(Sessions, Geometry);
        Geometry.Changed += Monitor.Reconcile;
        expiryTimer = new System.Threading.Timer(_ => { Sessions.Sweep(DateTimeOffset.UtcNow); Geometry.Poll(DateTimeOffset.UtcNow); }, null, 500, 500);
    }
    public static async Task<BridgeRuntime> StartAsync(BridgeOptions chrome)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders(); // Never log bootstrap capabilities or user URLs.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, 0);
            options.Limits.MaxRequestBodySize = 4096;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(5);
        });
        var server = builder.Build();
        var host = new BridgeRuntime(server, chrome);
        server.UseWebSockets();
        server.Use(async (context, next) =>
        {
            if (context.Request.Host.Host != "127.0.0.1") { context.Response.StatusCode = 400; return; }
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next(context);
        });
        server.MapGet(Prefix + "/bootstrap", (HttpContext context) => Results.Content($"""
            <!doctype html><html lang="en"><meta charset="utf-8">
            <meta name="referrer" content="no-referrer"><title>{(Guid.TryParse(context.Request.Query["session"], out var id) ? GeometryCoordinator.Marker(id) : "LazyChromeWindowBridge session launch")}</title>
            <h1>Opening your session</h1><p>LazyChromeWindowBridge will bind this window and open the launch URL.</p>
            <p>If this page remains, check the calling application and enable the extension in this Chrome profile.</p></html>
            """, "text/html"));
        server.MapGet(Prefix + "/monitor", (HttpContext context) => MonitorBridge.Handle(context, host));
        server.MapGet(Prefix + "/api/sessions/{id:guid}", (Guid id, HttpContext context) =>
        {
            if (!host.Authorized(context, id)) return Results.Unauthorized();
            var session = host.Sessions.Get(id)!;
            if (session.State is SessionState.Closed or SessionState.Failed) return Results.Conflict();
            return Results.Json(new { bridgeId = host.BridgeId, session.AppSessionId, session.LaunchUrl, nativeGeometry = true, monitoring = true });
        });
        server.MapPost(Prefix + "/api/sessions/{id:guid}/{action}", async (Guid id, string action, HttpContext context) =>
        {
            if (!host.Authorized(context, id)) return Results.Unauthorized();
            if (action is not ("bind" or "closed" or "native")) return Results.NotFound();
            if (!context.Request.HasJsonContentType()) return Results.StatusCode(415);
            WindowReport? report;
            try { report = await context.Request.ReadFromJsonAsync<WindowReport>(context.RequestAborted); }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(); }
            if (report is null || !host.Sessions.Report(id, report, action == "closed", DateTimeOffset.UtcNow)) return Results.Conflict();
            if (action == "native")
            {
                try { return await Task.Run(() => host.Geometry.EnsureMapped(id)) ? Results.Ok() : Results.StatusCode(503); }
                catch (Exception) { return Results.StatusCode(503); }
            }
            return Results.Ok();
        });
        try
        {
            await server.StartAsync();
            var addresses = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            host.BaseUri = new Uri(addresses.Addresses.Single());
            return host;
        }
        catch { await host.DisposeAsync(); throw; }
    }
    private bool Authorized(HttpContext context, Guid id)
    {
        // No CORS grant: arbitrary websites cannot send an authenticated JSON request.
        var authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.Ordinal) && Sessions.Authenticate(id, authorization[7..]);
    }
    internal (SessionSnapshot Session, Uri Bootstrap) PrepareLaunch(string url)
    {
        var (session, token) = Sessions.Create(url, DateTimeOffset.UtcNow);
        var bootstrap = new Uri(BaseUri, Prefix + $"/bootstrap?session={session.AppSessionId:D}#v=1&bridge={BridgeId:D}&session={session.AppSessionId:D}&token={token}");
        return (session, bootstrap);
    }
    public async Task<SessionSnapshot> LaunchAsync(string url)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        var (session, bootstrap) = PrepareLaunch(url);
        try { await Task.Run(() => ChromeLauncher.Launch(chrome, bootstrap)); }
        catch (Exception error) { Sessions.FailLaunch(session.AppSessionId, "Chrome launch failed: " + error.Message); }
        return Sessions.Get(session.AppSessionId)!;
    }
    public SessionSnapshot[] GetSessions() => Sessions.GetAll();
    public SessionSnapshot? GetSession(Guid appSessionId) => Sessions.Get(appSessionId);
    public WindowSnapshot? GetWindow(Guid appSessionId) => Geometry.Get(appSessionId);
    public WindowSnapshot SetWindowBounds(Guid appSessionId, PixelRect bounds) => Geometry.SetWindowBounds(appSessionId, bounds);
    public WindowSnapshot Park(Guid appSessionId) => Geometry.Park(appSessionId);
    public WindowSnapshot Restore(Guid appSessionId) => Geometry.Restore(appSessionId);
    public void StartMonitoring(CaptureOptions? options = null) => Monitor.Start(options ?? new());
    public void StopMonitoring() => Monitor.Stop();
    public MonitorSnapshot GetMonitorState() => Monitor.Snapshot();
    public MonitorFrame? GetLatestFrame(Guid appSessionId) => Monitor.Latest(appSessionId);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await expiryTimer.DisposeAsync();
        await Monitor.ShutdownAsync();
        await Task.Run(Geometry.Dispose);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await server.StopAsync(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { await server.DisposeAsync(); }
    }
}
