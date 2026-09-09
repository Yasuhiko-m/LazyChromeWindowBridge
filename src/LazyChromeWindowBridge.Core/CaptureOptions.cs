namespace LazyChromeWindowBridge.Core;

public sealed record CaptureOptions(int FramesPerSecond = 2, int MaxWidth = 240, int MaxHeight = 135)
{
    public void Validate()
    {
        if (FramesPerSecond is < 1 or > 10 || MaxWidth is < 160 or > 1920 || MaxHeight is < 90 or > 1080)
            throw new ArgumentException("Monitor supports 1–10 fps and output up to 1920×1080.");
    }
}
