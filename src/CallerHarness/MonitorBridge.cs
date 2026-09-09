using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CallerHarness;

internal static class MonitorBridge
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task Handle(HttpContext context, SessionHost host)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Guid id = default, connection = Guid.NewGuid();
        try
        {
            using var authentication = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            authentication.CancelAfter(TimeSpan.FromSeconds(3));
            using var hello = await Receive(socket, 4096, authentication.Token);
            if (hello is null || !hello.RootElement.TryGetProperty("appSessionId", out var idValue) || !idValue.TryGetGuid(out id) ||
                !hello.RootElement.TryGetProperty("token", out var tokenValue) || tokenValue.ValueKind != JsonValueKind.String ||
                !host.Sessions.Authenticate(id, tokenValue.GetString()!) || !host.Monitor.Connect(id, connection)) return;
            var receiver = ReceiveFrames();
            try
            {
                while (!receiver.IsCompleted && socket.State == WebSocketState.Open)
                {
                    var message = JsonSerializer.SerializeToUtf8Bytes(host.Monitor.Control(id), Json);
                    await socket.SendAsync(message, WebSocketMessageType.Text, true, lifetime.Token);
                    await Task.WhenAny(receiver, Task.Delay(500, lifetime.Token));
                    lifetime.Token.ThrowIfCancellationRequested();
                }
                await receiver;
            }
            finally { lifetime.Cancel(); try { await receiver; } catch (OperationCanceledException) { } }

            async Task ReceiveFrames()
            {
                while (socket.State == WebSocketState.Open)
                {
                    using var message = await Receive(socket, 3000000, lifetime.Token);
                    if (message is null) break;
                    var root = message.RootElement;
                    var generation = root.GetProperty("generation").GetInt64();
                    if (root.GetProperty("type").GetString() == "frame")
                    {
                        try { host.Monitor.Accept(id, connection, generation, root.GetProperty("windowId").GetInt32(), root.GetProperty("tabId").GetInt32(),
                            root.GetProperty("data").GetString()!, root.GetProperty("captureMilliseconds").GetDouble()); }
                        catch (Exception e) when (e is ArgumentException or FormatException or OutOfMemoryException)
                        { host.Monitor.Status(id, connection, generation, "Invalid monitor image.", false); }
                    }
                    else host.Monitor.Status(id, connection, generation, root.TryGetProperty("error", out var error) ? error.GetString() : null,
                        root.TryGetProperty("capturing", out var capturing) && capturing.GetBoolean());
                }
            }
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { }
        finally { lifetime.Cancel(); host.Monitor.Disconnect(id, connection); socket.Abort(); }
    }
    private static async Task<JsonDocument?> Receive(WebSocket socket, int maximum, CancellationToken token)
    {
        using var data = new MemoryStream();
        var buffer = new byte[16384];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text || data.Length + result.Count > maximum) throw new JsonException("Invalid monitor message size/type.");
            data.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return JsonDocument.Parse(data.ToArray());
    }
}
