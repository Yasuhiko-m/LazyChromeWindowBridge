namespace LazyChromeWindowBridge.Core;

/// <summary>Requested JPEG cadence and output bounds; these never resize Chrome or its viewport.</summary>
/// <remarks>1–30 fps is a request ceiling, not a throughput guarantee. JPEG quality is fixed at 70.</remarks>
public sealed record CaptureOptions(int FramesPerSecond = 2, int MaxWidth = 240, int MaxHeight = 135)
{
    public void Validate()
    {
        if (FramesPerSecond is < 1 or > 30 || MaxWidth is < 160 or > 1920 || MaxHeight is < 90 or > 1080)
            throw new ArgumentException("Monitor supports 1–30 fps and output up to 1920×1080.");
    }
}
