namespace LazyChromeWindowBridge.Core;

internal sealed class TaskbarCoordinator(GeometryCoordinator geometry, INativeWindows native) : IDisposable
{
    private const long AppWindow = 0x00040000;
    private const long ToolWindow = 0x00000080;
    private readonly object gate = new();
    private readonly Dictionary<Guid, (NativeIdentity Identity, long OriginalStyle)> changed = [];
    private readonly List<TaskbarRestorationEvidence> restorationResults = [];
    private bool disposed;
    internal TaskbarRestorationEvidence[] RestorationResults { get { lock (gate) return restorationResults.ToArray(); } }

    public void Set(Guid appSessionId, bool show)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var identity = geometry.RequireOwned(appSessionId);
            if (!native.Alive(identity)) throw new InvalidOperationException("Taskbar control requires the exact live owned native window.");
            if (show)
            {
                if (!changed.TryGetValue(appSessionId, out var saved)) return;
                if (saved.Identity != identity) throw new InvalidOperationException("Native session identity changed; taskbar state was not retargeted.");
                native.WriteExtendedStyle(identity, saved.OriginalStyle);
                changed.Remove(appSessionId);
                return;
            }
            if (changed.TryGetValue(appSessionId, out var existing))
            {
                if (existing.Identity != identity) throw new InvalidOperationException("Native session identity changed; taskbar state was not retargeted.");
                return;
            }
            var original = native.ReadExtendedStyle(identity);
            try
            {
                native.WriteExtendedStyle(identity, (original & ~AppWindow) | ToolWindow);
            }
            catch
            {
                // A shell refresh can fail after SetWindowLongPtr succeeds. Roll the
                // exact identity back before leaving it outside our restoration book.
                try
                {
                    if (native.Alive(identity)) native.WriteExtendedStyle(identity, original);
                }
                catch { }
                throw;
            }
            changed.Add(appSessionId, (identity, original));
        }
    }

    internal TaskbarStateEvidence Evidence(Guid appSessionId)
    {
        lock (gate)
        {
            var identity = geometry.RequireOwned(appSessionId);
            var current = native.ReadExtendedStyle(identity);
            var hidden = changed.TryGetValue(appSessionId, out var saved) && saved.Identity == identity;
            return new(appSessionId, identity, hidden, hidden ? saved.OriginalStyle : current, current);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            foreach (var saved in changed.Values)
            {
                try
                {
                    if (native.Alive(saved.Identity))
                    {
                        native.WriteExtendedStyle(saved.Identity, saved.OriginalStyle);
                        restorationResults.Add(new(saved.Identity, saved.OriginalStyle, native.ReadExtendedStyle(saved.Identity), true));
                    }
                }
                catch { /* Geometry shutdown records ownership failures; never retarget taskbar restoration. */ }
            }
            changed.Clear();
        }
    }
}

internal sealed record TaskbarStateEvidence(Guid AppSessionId, NativeIdentity Identity, bool Hidden, long OriginalStyle, long CurrentStyle);
internal sealed record TaskbarRestorationEvidence(NativeIdentity Identity, long OriginalStyle, long RestoredStyle, bool Restored);
