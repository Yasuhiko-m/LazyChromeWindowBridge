namespace LazyChromeWindowBridge.Core;

public sealed record MonitorFrame(Guid AppSessionId, NativeIdentity Identity, int WindowId, int TabId,
    long Generation, long Sequence, int Width, int Height, ReadOnlyMemory<byte> Jpeg, DateTimeOffset ReceivedAt, double CaptureMilliseconds)
{
    /// <summary>The source used for this frame. NativeWindow frames use TabId -1 because tab identity is not applicable.</summary>
    public CaptureMode Mode { get; init; } = CaptureMode.BrowserViewport;
}
