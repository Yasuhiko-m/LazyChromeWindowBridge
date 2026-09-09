namespace LazyChromeWindowBridge.Core;

internal sealed record MonitorGeometry(string Device, PixelRect Bounds, PixelRect WorkArea, bool Primary, uint DpiX, uint DpiY, uint ScalePercent = 0);
internal static class GeometryMath
{
    public static bool Reachable(PixelRect rect, IReadOnlyList<MonitorGeometry> monitors) => rect.Valid && monitors.Any(m =>
        Math.Min(rect.Right, m.WorkArea.Right) - Math.Max(rect.Left, m.WorkArea.Left) >= 100 &&
        rect.Top >= m.WorkArea.Top - 12 && rect.Top + 40 <= m.WorkArea.Bottom);
    public static PixelRect VisibleFallback(PixelRect rect, IReadOnlyList<MonitorGeometry> monitors)
    {
        if (monitors.Count == 0) throw new InvalidOperationException("No active monitor is available.");
        if (Reachable(rect, monitors)) return rect;
        var work = monitors.OrderByDescending(m => Math.Max(0, Math.Min(rect.Right, m.WorkArea.Right) - Math.Max(rect.Left, m.WorkArea.Left)) *
                Math.Max(0, Math.Min(rect.Bottom, m.WorkArea.Bottom) - Math.Max(rect.Top, m.WorkArea.Top)))
            .ThenByDescending(m => m.Primary).ThenBy(m => m.Device, StringComparer.Ordinal).First().WorkArea;
        var width = Math.Clamp(rect.Width, Math.Min(300, work.Width), work.Width);
        var height = Math.Clamp(rect.Height, Math.Min(200, work.Height), work.Height);
        return new PixelRect(Math.Clamp(rect.Left, work.Left, checked((int)work.Right) - width),
            Math.Clamp(rect.Top, work.Top, checked((int)work.Bottom) - height), width, height);
    }
    public static PixelRect Park(PixelRect normal, IReadOnlyList<MonitorGeometry> monitors)
    {
        if (!normal.Valid || monitors.Count == 0) throw new InvalidOperationException("Cannot compute PARK without valid geometry and monitors.");
        var top = checked(monitors.Min(m => m.Bounds.Top) - normal.Height - 64);
        var left = monitors.FirstOrDefault(m => m.Primary)?.Bounds.Left ?? monitors[0].Bounds.Left;
        var parked = new PixelRect(left, top, normal.Width, normal.Height);
        if (!parked.Valid || monitors.Any(m => parked.Intersects(m.Bounds))) throw new InvalidOperationException("PARK rectangle is not safely outside all monitors.");
        return parked;
    }
}
