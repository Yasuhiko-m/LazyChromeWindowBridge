using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LazyChromeWindowBridge.Core;

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
