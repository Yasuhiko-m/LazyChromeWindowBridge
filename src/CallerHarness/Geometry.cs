using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CallerHarness;

internal sealed record PixelRect(int Left, int Top, int Width, int Height)
{
    public long Right => (long)Left + Width;
    public long Bottom => (long)Top + Height;
    public bool Valid => Width >= 100 && Height >= 80 && Width <= 100000 && Height <= 100000 && Math.Abs((long)Left) <= 1000000 && Math.Abs((long)Top) <= 1000000;
    public bool Intersects(PixelRect other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    public bool Near(PixelRect other, int tolerance = 2) => Math.Abs((long)Left - other.Left) <= tolerance && Math.Abs((long)Top - other.Top) <= tolerance && Math.Abs((long)Width - other.Width) <= tolerance && Math.Abs((long)Height - other.Height) <= tolerance;
}
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
internal sealed record GeometryProfile(int SchemaVersion, string LaunchUrl, PixelRect Normal, uint Dpi, DateTimeOffset SavedAt);
internal sealed class GeometryStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string FilePath(string url) => Path.Combine(DirectoryPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".json");
    public GeometryProfile? Load(string url)
    {
        try
        {
            var record = JsonSerializer.Deserialize<GeometryProfile>(File.ReadAllText(FilePath(url)));
            return record is { SchemaVersion: 1, Normal.Valid: true } && record.LaunchUrl == url ? record : null;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (JsonException) { return null; }
    }
    public void Save(string url, PixelRect normal, uint dpi)
    {
        if (!normal.Valid) throw new ArgumentException("Invalid normal geometry.");
        Directory.CreateDirectory(DirectoryPath);
        var destination = FilePath(url);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new GeometryProfile(1, url, normal, dpi, DateTimeOffset.UtcNow)));
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
