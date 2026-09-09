namespace LazyChromeWindowBridge.Core;

public sealed record MonitorFrame(Guid AppSessionId, NativeIdentity Identity, int WindowId, int TabId,
    long Generation, long Sequence, int Width, int Height, ReadOnlyMemory<byte> Jpeg, DateTimeOffset ReceivedAt, double CaptureMilliseconds);
