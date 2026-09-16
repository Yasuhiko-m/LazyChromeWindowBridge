using System.Text.Json.Serialization;

namespace LazyChromeWindowBridge.Core;

/// <summary>Requested JPEG cadence, output bounds and source; these never resize Chrome or its viewport.</summary>
/// <remarks>1–30 fps is a request ceiling, not a throughput guarantee. JPEG quality is fixed at 70.</remarks>
public sealed record CaptureOptions(int FramesPerSecond = 2, int MaxWidth = 240, int MaxHeight = 135)
{
    public CaptureMode Mode { get; init; } = CaptureMode.BrowserViewport;
    public CaptureRegion Region { get; init; } = new();
    public CaptureResize? Resize { get; init; }
    [JsonConstructor]
    public CaptureOptions(int framesPerSecond, int maxWidth, int maxHeight, CaptureMode mode)
        : this(framesPerSecond, maxWidth, maxHeight) => Mode = mode;

    public void Validate()
    {
        if (FramesPerSecond is < 1 or > 30 || MaxWidth is < 160 or > 1920 || MaxHeight is < 90 or > 1080)
            throw new ArgumentException("Monitor supports 1–30 fps and output up to 1920×1080.");
        if (!Enum.IsDefined(Mode)) throw new ArgumentException("Unknown capture mode.");
        Region.Validate();
        Resize?.Validate();
    }
}

/// <summary>A rectangular, zero-based selection in a bounded logical grid.</summary>
public sealed record CaptureRegion(int Columns = 1, int Rows = 1, int Column = 0, int Row = 0, int ColumnSpan = 1, int RowSpan = 1)
{
    public void Validate()
    {
        if (Columns is < 1 or > 64 || Rows is < 1 or > 64 || Column < 0 || Row < 0 || ColumnSpan < 1 || RowSpan < 1 ||
            Column > Columns - ColumnSpan || Row > Rows - RowSpan)
            throw new ArgumentException("Capture region must be a bounded in-grid positive span.");
    }
}

public enum CaptureResizeFilter { NearestNeighbor = 0, Bilinear = 1, Bicubic = 2 }

/// <summary>Optional output dimensions after region selection. A missing dimension preserves aspect ratio.</summary>
public sealed record CaptureResize(int? Width = null, int? Height = null, CaptureResizeFilter Filter = CaptureResizeFilter.Bilinear)
{
    public void Validate()
    {
        if (Width is <= 0 || Height is <= 0 || !Enum.IsDefined(Filter)) throw new ArgumentException("Capture resize dimensions and filter are invalid.");
        if (Width is > 8192 || Height is > 8192 || (Width is not null && Height is not null && (long)Width * Height > 24_000_000))
            throw new ArgumentException("Capture resize exceeds the safe image envelope.");
    }
}

/// <summary>Point-in-time natural source extent. BrowserViewport values are CSS pixels; NativeWindow values are native pixels.</summary>
public sealed record CaptureSourceSize(double Width, double Height);

internal static class CaptureSizing
{
    internal static (int X, int Y, int Width, int Height) RegionBounds(int width, int height, CaptureRegion region)
    {
        region.Validate();
        if (width < 1 || height < 1) throw new ArgumentException("Capture source is empty.");
        var x = (int)((long)width * region.Column / region.Columns); var y = (int)((long)height * region.Row / region.Rows);
        var right = (int)((long)width * (region.Column + region.ColumnSpan) / region.Columns);
        var bottom = (int)((long)height * (region.Row + region.RowSpan) / region.Rows);
        if (right <= x || bottom <= y) throw new ArgumentException("Capture region resolves to no source pixels.");
        return (x, y, right - x, bottom - y);
    }
    internal static (int Width, int Height) OutputSize(int sourceWidth, int sourceHeight, CaptureOptions options)
    {
        var region = RegionBounds(sourceWidth, sourceHeight, options.Region);
        if (options.Resize is { } resize)
        {
            resize.Validate(); int width, height;
            if (resize.Width is { } w && resize.Height is { } h) (width, height) = (w, h);
            else if (resize.Width is { } wOnly) (width, height) = (wOnly, Math.Max(1, (int)Math.Round((double)region.Height * wOnly / region.Width)));
            else if (resize.Height is { } hOnly) (width, height) = (Math.Max(1, (int)Math.Round((double)region.Width * hOnly / region.Height)), hOnly);
            else (width, height) = (region.Width, region.Height);
            if (width > 8192 || height > 8192 || (long)width * height > 24_000_000) throw new ArgumentException("Capture output exceeds the safe image envelope.");
            return (width, height);
        }
        var ratio = Math.Min(1d, Math.Min((double)options.MaxWidth / region.Width, (double)options.MaxHeight / region.Height));
        return (Math.Max(1, (int)(region.Width * ratio)), Math.Max(1, (int)(region.Height * ratio)));
    }
}
