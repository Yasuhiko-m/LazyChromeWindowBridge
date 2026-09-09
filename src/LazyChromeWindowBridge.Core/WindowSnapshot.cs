namespace LazyChromeWindowBridge.Core;

public enum PlacementState { Visible, Parking, Parked, Restoring, Closed }
public sealed record WindowSnapshot(Guid AppSessionId, int? WindowId, string LaunchUrl, NativeIdentity Identity,
    PlacementState State, PixelRect Normal, PixelRect? Current, uint Dpi, string? Error, long PlacementGeneration);
