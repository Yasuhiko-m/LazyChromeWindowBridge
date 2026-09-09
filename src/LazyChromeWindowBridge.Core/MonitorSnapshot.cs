namespace LazyChromeWindowBridge.Core;

public sealed record SessionMonitorSnapshot(Guid AppSessionId, int? WindowId, NativeIdentity? Identity, long Generation,
    string State, long Frames, long Bytes, DateTimeOffset? LastFrameAt, int Width, int Height, string? Error, bool Connected, bool Capturing);
public sealed record MonitorSnapshot(bool Enabled, long Frames, long Bytes, int Connections, int CapturingConnections, SessionMonitorSnapshot[] Sessions);
